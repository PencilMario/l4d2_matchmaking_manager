import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GlobalSettingsView } from './GlobalSettingsView';

type PauseWindow = { start: string; end: string };
type SettingsFixture = {
  steamProxyUrl: string | null;
  steamWebApiKeyConfigured: boolean;
  warmupSchedulingEnabled: boolean;
  warmupPauseWindows: PauseWindow[];
  warmupPauseWindowsActive: boolean;
  updatedAt: string;
};

const settingsFixture = (overrides: Partial<SettingsFixture> = {}): SettingsFixture => ({
  steamProxyUrl: null,
  steamWebApiKeyConfigured: false,
  warmupSchedulingEnabled: true,
  warmupPauseWindows: [],
  warmupPauseWindowsActive: false,
  updatedAt: '2026-08-19T09:00:00Z',
  ...overrides,
});

const renderView = (overrides: Record<string, unknown> = {}) => {
  const View = GlobalSettingsView as any;
  return render(<View
    load={() => Promise.resolve(settingsFixture())}
    saveProxy={vi.fn()}
    saveKey={vi.fn()}
    saveWarmupScheduling={vi.fn().mockResolvedValue({ enabled: true, updatedAt: '2026-08-19T10:00:00Z' })}
    saveWarmupPauseWindows={vi.fn().mockResolvedValue({ windows: [], active: false, updatedAt: '2026-08-19T10:00:00Z' })}
    {...overrides}
  />);
};

describe('全局设置', () => {
  it('renders four independent setting cards', async () => {
    renderView();

    await screen.findByRole('heading', { name: '暖服和调度' });

    expect(screen.getAllByRole('article')).toHaveLength(4);
    expect(screen.getByRole('heading', { name: '暖服和调度' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'VNC 代理' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Steam Web API Key' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '暖服暂停时间段' })).toBeInTheDocument();
  });

  it('loads and saves the Steam proxy without touching pause windows', async () => {
    const saveProxy = vi.fn().mockResolvedValue({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    const saveKey = vi.fn();
    const saveWarmupPauseWindows = vi.fn();
    renderView({ saveProxy, saveKey, saveWarmupPauseWindows });

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 VNC 代理' }));

    await waitFor(() => expect(saveProxy).toHaveBeenCalledWith({ proxyUrl: 'http://127.0.0.1:7890' }));
    expect(saveKey).not.toHaveBeenCalled();
    expect(saveWarmupPauseWindows).not.toHaveBeenCalled();
    expect(await screen.findByText('VNC 代理已保存。')).toBeInTheDocument();
  });

  it('does not reveal, replaces, and clears the Steam Web API key', async () => {
    const saveProxy = vi.fn();
    const saveKey = vi.fn().mockResolvedValue({ configured: true, updatedAt: '2026-08-19T10:00:00Z' });
    renderView({
      load: () => Promise.resolve(settingsFixture({ steamWebApiKeyConfigured: true })),
      saveProxy,
      saveKey,
    });

    const key = await screen.findByPlaceholderText('已配置，输入新 Key 以替换');
    expect(key).toHaveAttribute('type', 'password');
    expect(key).toHaveValue('');
    fireEvent.change(key, { target: { value: 'replacement-key' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 Steam Web API Key' }));
    await waitFor(() => expect(saveKey).toHaveBeenCalledWith({ apiKey: 'replacement-key' }));
    expect(saveProxy).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: '清除 Steam Web API Key' }));
    await waitFor(() => expect(saveKey).toHaveBeenLastCalledWith({ clear: true }));
  });

  it('confirms before disabling global warmup scheduling', async () => {
    const saveWarmupScheduling = vi.fn().mockResolvedValue({ enabled: false, updatedAt: '2026-08-20T10:00:00Z' });
    renderView({ saveWarmupScheduling });

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    expect(toggle).toBeChecked();
    fireEvent.click(toggle);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(saveWarmupScheduling).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: '取消' }));
    expect(toggle).toBeChecked();
    fireEvent.click(toggle);
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    await waitFor(() => expect(saveWarmupScheduling).toHaveBeenCalledWith({ enabled: false }));
    expect(toggle).not.toBeChecked();
  });

  it('keeps the previous switch state and maps a drain conflict', async () => {
    const saveWarmupScheduling = vi.fn().mockRejectedValue(new Error('global_warmup_drain_failed'));
    renderView({ saveWarmupScheduling });

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    expect(await screen.findByText('暖服和调度仍保持禁用，但有任务未能确认停止，请检查任务状态后重试。')).toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it('adds, removes, validates, and saves multiple pause windows independently', async () => {
    const savePauseWindows = vi.fn().mockResolvedValue({
      windows: [{ start: '00:00', end: '08:00' }],
      active: true,
      updatedAt: '2026-08-23T10:00:00Z',
    });
    const saveProxy = vi.fn();
    const saveKey = vi.fn();
    const saveWarmupScheduling = vi.fn();
    renderView({ saveProxy, saveKey, saveWarmupScheduling, saveWarmupPauseWindows: savePauseWindows });

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

    await waitFor(() => expect(savePauseWindows).toHaveBeenCalledWith({ windows: [{ start: '00:00', end: '08:00' }] }));
    expect(saveProxy).not.toHaveBeenCalled();
    expect(saveKey).not.toHaveBeenCalled();
    expect(saveWarmupScheduling).not.toHaveBeenCalled();
    expect(await screen.findByText('暖服暂停时间段已保存。')).toBeInTheDocument();
    expect(screen.getByText('当前状态：正在暂停')).toBeInTheDocument();
  });

  it('rejects an incomplete pause window before calling the API', async () => {
    const savePauseWindows = vi.fn();
    renderView({ saveWarmupPauseWindows: savePauseWindows });

    await screen.findByRole('heading', { name: '暖服暂停时间段' });
    fireEvent.click(screen.getByRole('button', { name: '新增时间段' }));
    fireEvent.change(screen.getByLabelText('开始时间 1'), { target: { value: '23:00' } });
    fireEvent.click(screen.getByRole('button', { name: '保存暖服暂停时间段' }));

    expect(await screen.findByText('请填写完整的时间段。')).toBeInTheDocument();
    expect(savePauseWindows).not.toHaveBeenCalled();
  });

  it('enables scheduling directly when the switch is currently off', async () => {
    const saveWarmupScheduling = vi.fn().mockResolvedValue({ enabled: true, updatedAt: '2026-08-20T10:00:00Z' });
    renderView({
      load: () => Promise.resolve(settingsFixture({ warmupSchedulingEnabled: false })),
      saveWarmupScheduling,
    });

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    expect(toggle).not.toBeChecked();
    fireEvent.click(toggle);

    await waitFor(() => expect(saveWarmupScheduling).toHaveBeenCalledWith({ enabled: true }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(toggle).toBeChecked();
  });
});
