import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SettingsWorkspace } from './SettingsWorkspace';

describe('全局设置', () => {
  it('does not reveal a configured Steam Web API key and can replace it', async () => {
    const updateProxy = vi.fn();
    const updateKey = vi.fn().mockResolvedValue({ configured: true, updatedAt: '2026-08-19T10:00:00Z' });
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} updateProxy={updateProxy} updateKey={updateKey} />);

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
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} updateProxy={updateProxy} updateKey={updateKey} />);

    await screen.findByLabelText('Steam Web API Key');
    fireEvent.click(screen.getByRole('button', { name: '清除 Steam Web API Key' }));

    await waitFor(() => expect(updateKey).toHaveBeenCalledWith({ clear: true }));
    expect(updateProxy).not.toHaveBeenCalled();
  });

  it('saves the VNC proxy without sending the API key', async () => {
    const updateProxy = vi.fn().mockResolvedValue({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    const updateKey = vi.fn();
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} updateProxy={updateProxy} updateKey={updateKey} />);

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存 VNC 代理' }));

    await waitFor(() => expect(updateProxy).toHaveBeenCalledWith({ proxyUrl: 'http://127.0.0.1:7890' }));
    expect(updateKey).not.toHaveBeenCalled();
  });
});
