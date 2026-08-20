import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SettingsWorkspace } from './SettingsWorkspace';

describe('全局设置', () => {
  it('does not reveal a configured Steam Web API key and can replace it', async () => {
    const updateProxy = vi.fn();
    const updateKey = vi.fn().mockResolvedValue({ configured: true, updatedAt: '2026-08-19T10:00:00Z' });
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, warmupSchedulingEnabled: true, updatedAt: '2026-08-19T09:00:00Z' })} updateProxy={updateProxy} updateKey={updateKey} updateWarmupScheduling={vi.fn()} />);

    const input = await screen.findByLabelText('Steam Web API Key');
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
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, warmupSchedulingEnabled: true, updatedAt: '2026-08-19T09:00:00Z' })} updateProxy={updateProxy} updateKey={updateKey} updateWarmupScheduling={vi.fn()} />);

    await screen.findByLabelText('Steam Web API Key');
    fireEvent.click(screen.getByRole('button', { name: '清除 Steam Web API Key' }));

    await waitFor(() => expect(updateKey).toHaveBeenCalledWith({ clear: true }));
    expect(updateProxy).not.toHaveBeenCalled();
  });

  it('saves the VNC proxy without sending the API key', async () => {
    const updateProxy = vi.fn().mockResolvedValue({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    const updateKey = vi.fn();
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, warmupSchedulingEnabled: true, updatedAt: '2026-08-19T09:00:00Z' })} updateProxy={updateProxy} updateKey={updateKey} updateWarmupScheduling={vi.fn()} />);

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 VNC 代理' }));

    await waitFor(() => expect(updateProxy).toHaveBeenCalledWith({ proxyUrl: 'http://127.0.0.1:7890' }));
    expect(updateKey).not.toHaveBeenCalled();
  });

  it('confirms before disabling global warmup scheduling', async () => {
    const updateWarmupScheduling = vi.fn().mockResolvedValue({ enabled: false, updatedAt: '2026-08-20T10:00:00Z' });
    const Workspace = SettingsWorkspace as any;
    render(<Workspace
      getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: true, updatedAt: '2026-08-20T09:00:00Z' })}
      updateProxy={vi.fn()}
      updateKey={vi.fn()}
      updateWarmupScheduling={updateWarmupScheduling}
    />);

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
    render(<SettingsWorkspace
      getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: false, updatedAt: '2026-08-20T09:00:00Z' })}
      updateProxy={vi.fn()}
      updateKey={vi.fn()}
      updateWarmupScheduling={updateWarmupScheduling}
    />);

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);

    await waitFor(() => expect(updateWarmupScheduling).toHaveBeenCalledWith({ enabled: true }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it('keeps the previous switch state and maps a drain conflict', async () => {
    const updateWarmupScheduling = vi.fn().mockRejectedValue(new Error('global_warmup_drain_failed'));
    render(<SettingsWorkspace
      getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: true, updatedAt: '2026-08-20T09:00:00Z' })}
      updateProxy={vi.fn()}
      updateKey={vi.fn()}
      updateWarmupScheduling={updateWarmupScheduling}
    />);

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    expect(await screen.findByText('暖服和调度仍保持禁用，但有任务未能确认停止，请检查任务状态后重试。')).toBeInTheDocument();
    expect(toggle).toBeChecked();
  });
});
