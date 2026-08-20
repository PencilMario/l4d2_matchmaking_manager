import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GlobalSettingsView } from './GlobalSettingsView';

describe('全局设置', () => {
  it('loads and saves the Steam proxy', async () => {
    const saveProxy = vi.fn().mockResolvedValue({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    const saveKey = vi.fn();
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: true, updatedAt: '2026-08-19T09:00:00Z' })} saveProxy={saveProxy} saveKey={saveKey} saveWarmupScheduling={vi.fn()} />);

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 VNC 代理' }));

    await waitFor(() => expect(saveProxy).toHaveBeenCalledWith({ proxyUrl: 'http://127.0.0.1:7890' }));
    expect(saveKey).not.toHaveBeenCalled();
    expect(await screen.findByText('VNC 代理已保存。')).toBeInTheDocument();
  });

  it('does not reveal, replaces, and clears the Steam Web API key', async () => {
    const saveProxy = vi.fn();
    const saveKey = vi.fn().mockResolvedValue({ configured: true, updatedAt: '2026-08-19T10:00:00Z' });
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, warmupSchedulingEnabled: true, updatedAt: '2026-08-19T09:00:00Z' })} saveProxy={saveProxy} saveKey={saveKey} saveWarmupScheduling={vi.fn()} />);

    const key = await screen.findByLabelText('Steam Web API Key');
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
    const View = GlobalSettingsView as any;
    render(<View
      load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: true, updatedAt: '2026-08-20T09:00:00Z' })}
      saveProxy={vi.fn()}
      saveKey={vi.fn()}
      saveWarmupScheduling={saveWarmupScheduling}
    />);

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
    render(<GlobalSettingsView
      load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: true, updatedAt: '2026-08-20T09:00:00Z' })}
      saveProxy={vi.fn()}
      saveKey={vi.fn()}
      saveWarmupScheduling={saveWarmupScheduling}
    />);

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    fireEvent.click(toggle);
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    expect(await screen.findByText('暖服和调度仍保持禁用，但有任务未能确认停止，请检查任务状态后重试。')).toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it('enables scheduling directly when the switch is currently off', async () => {
    const saveWarmupScheduling = vi.fn().mockResolvedValue({ enabled: true, updatedAt: '2026-08-20T10:00:00Z' });
    render(<GlobalSettingsView
      load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, warmupSchedulingEnabled: false, updatedAt: '2026-08-20T09:00:00Z' })}
      saveProxy={vi.fn()}
      saveKey={vi.fn()}
      saveWarmupScheduling={saveWarmupScheduling}
    />);

    const toggle = await screen.findByRole('checkbox', { name: '全局启用暖服和调度' });
    expect(toggle).not.toBeChecked();
    fireEvent.click(toggle);

    await waitFor(() => expect(saveWarmupScheduling).toHaveBeenCalledWith({ enabled: true }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(toggle).toBeChecked();
  });
});
