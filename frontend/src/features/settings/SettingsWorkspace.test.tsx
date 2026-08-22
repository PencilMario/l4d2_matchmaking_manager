import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { GlobalSettings } from '../../api/models';
import { SettingsWorkspace } from './SettingsWorkspace';

const settingsFixture = (overrides: Partial<GlobalSettings> = {}): GlobalSettings => ({
  steamProxyUrl: null,
  steamWebApiKeyConfigured: false,
  warmupSchedulingEnabled: true,
  warmupPauseWindows: [],
  warmupPauseWindowsActive: false,
  updatedAt: '2026-08-19T09:00:00Z',
  ...overrides,
});

const renderWorkspace = (overrides: Record<string, unknown> = {}) => {
  const props = {
    getSettings: () => Promise.resolve(settingsFixture()),
    updateProxy: vi.fn(),
    updateKey: vi.fn(),
    updateWarmupScheduling: vi.fn().mockResolvedValue({ enabled: true, updatedAt: '2026-08-19T10:00:00Z' }),
    updateWarmupPauseWindows: vi.fn().mockResolvedValue({ windows: [], active: false, updatedAt: '2026-08-19T10:00:00Z' }),
    ...overrides,
  };
  return render(<SettingsWorkspace {...props} />);
};

describe('全局设置', () => {
  it('renders four independent setting cards', async () => {
    renderWorkspace();

    await screen.findByRole('heading', { name: '暖服和调度' });

    expect(screen.getAllByRole('article')).toHaveLength(4);
    expect(screen.getByRole('heading', { name: '暖服和调度' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'VNC 代理' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Steam Web API Key' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '暖服暂停时间段' })).toBeInTheDocument();
  });

  it('does not reveal a configured Steam Web API key and can replace it', async () => {
    const updateProxy = vi.fn();
    const updateKey = vi.fn().mockResolvedValue({ configured: true, updatedAt: '2026-08-19T10:00:00Z' });
    renderWorkspace({
      getSettings: () => Promise.resolve(settingsFixture({ steamWebApiKeyConfigured: true })),
      updateProxy,
      updateKey,
    });

    const input = await screen.findByPlaceholderText('已配置，输入新 Key 以替换');
    expect(input).toHaveAttribute('type', 'password');
    expect(input).toHaveValue('');
    expect(screen.getByText('当前状态：已配置')).toBeInTheDocument();
    fireEvent.change(input, { target: { value: 'new-secret-key' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 Steam Web API Key' }));

    await waitFor(() => expect(updateKey).toHaveBeenCalledWith({ apiKey: 'new-secret-key' }));
    expect(updateProxy).not.toHaveBeenCalled();
  });

  it('clears the Steam Web API key with an explicit action', async () => {
    const updateProxy = vi.fn();
    const updateKey = vi.fn().mockResolvedValue({ configured: false, updatedAt: '2026-08-19T10:00:00Z' });
    renderWorkspace({
      getSettings: () => Promise.resolve(settingsFixture({ steamWebApiKeyConfigured: true })),
      updateProxy,
      updateKey,
    });

    await screen.findByPlaceholderText('已配置，输入新 Key 以替换');
    fireEvent.click(screen.getByRole('button', { name: '清除 Steam Web API Key' }));

    await waitFor(() => expect(updateKey).toHaveBeenCalledWith({ clear: true }));
    expect(updateProxy).not.toHaveBeenCalled();
  });

  it('saves the VNC proxy without sending the API key', async () => {
    const updateProxy = vi.fn().mockResolvedValue({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    const updateKey = vi.fn();
    const updateWarmupPauseWindows = vi.fn();
    renderWorkspace({ updateProxy, updateKey, updateWarmupPauseWindows });

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 VNC 代理' }));

    await waitFor(() => expect(updateProxy).toHaveBeenCalledWith({ proxyUrl: 'http://127.0.0.1:7890' }));
    expect(updateKey).not.toHaveBeenCalled();
    expect(updateWarmupPauseWindows).not.toHaveBeenCalled();
  });

  it('confirms before disabling global warmup scheduling', async () => {
    const updateWarmupScheduling = vi.fn().mockResolvedValue({ enabled: false, updatedAt: '2026-08-20T10:00:00Z' });
    renderWorkspace({ updateWarmupScheduling });

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(updateWarmupScheduling).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    await waitFor(() => expect(updateWarmupScheduling).toHaveBeenCalledWith({ enabled: false }));
    expect(toggle).not.toBeChecked();
  });

  it('enables scheduling directly when the switch is currently off', async () => {
    const updateWarmupScheduling = vi.fn().mockResolvedValue({ enabled: true, updatedAt: '2026-08-20T10:00:00Z' });
    renderWorkspace({
      getSettings: () => Promise.resolve(settingsFixture({ warmupSchedulingEnabled: false })),
      updateWarmupScheduling,
    });

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);

    await waitFor(() => expect(updateWarmupScheduling).toHaveBeenCalledWith({ enabled: true }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it('keeps the previous switch state and maps a drain conflict', async () => {
    const updateWarmupScheduling = vi.fn().mockRejectedValue(new Error('global_warmup_drain_failed'));
    renderWorkspace({ updateWarmupScheduling });

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    expect(await screen.findByText('暖服和调度仍保持禁用，但有任务未能确认停止，请检查任务状态后重试。')).toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it('adds, removes, validates, and saves multiple pause windows independently', async () => {
    const updatePauseWindows = vi.fn().mockResolvedValue({
      windows: [{ start: '00:00', end: '08:00' }],
      active: true,
      updatedAt: '2026-08-23T10:00:00Z',
    });
    const updateProxy = vi.fn();
    const updateKey = vi.fn();
    const updateWarmupScheduling = vi.fn();
    renderWorkspace({ updateProxy, updateKey, updateWarmupScheduling, updateWarmupPauseWindows: updatePauseWindows });

    expect(await screen.findByText('暂未设置暂停时间段')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '新增时间段' }));
    fireEvent.change(screen.getByLabelText('开始时间 1'), { target: { value: '23:00' } });
    fireEvent.change(screen.getByLabelText('结束时间 1'), { target: { value: '00:00' } });
    fireEvent.click(screen.getByRole('button', { name: '新增时间段' }));
    fireEvent.change(screen.getByLabelText('开始时间 2'), { target: { value: '00:00' } });
    fireEvent.change(screen.getByLabelText('结束时间 2'), { target: { value: '08:00' } });
    fireEvent.click(screen.getByRole('button', { name: '删除时间段 1' }));

    expect(screen.getByLabelText('开始时间 1')).toHaveValue('00:00');
    expect(screen.getByLabelText('结束时间 1')).toHaveValue('08:00');
    fireEvent.click(screen.getByRole('button', { name: '保存暖服暂停时间段' }));

    await waitFor(() => expect(updatePauseWindows).toHaveBeenCalledWith({ windows: [{ start: '00:00', end: '08:00' }] }));
    expect(updateProxy).not.toHaveBeenCalled();
    expect(updateKey).not.toHaveBeenCalled();
    expect(updateWarmupScheduling).not.toHaveBeenCalled();
    expect(await screen.findByText('暖服暂停时间段已保存。')).toBeInTheDocument();
    expect(screen.getByText('当前状态：正在暂停')).toBeInTheDocument();
  });

  it('rejects an incomplete pause window before calling the API', async () => {
    const updatePauseWindows = vi.fn();
    renderWorkspace({ updateWarmupPauseWindows: updatePauseWindows });

    await screen.findByRole('heading', { name: '暖服暂停时间段' });
    fireEvent.click(screen.getByRole('button', { name: '新增时间段' }));
    fireEvent.change(screen.getByLabelText('开始时间 1'), { target: { value: '23:00' } });
    fireEvent.click(screen.getByRole('button', { name: '保存暖服暂停时间段' }));

    expect(await screen.findByText('请填写完整的时间段。')).toBeInTheDocument();
    expect(updatePauseWindows).not.toHaveBeenCalled();
  });
});
