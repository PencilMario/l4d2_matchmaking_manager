import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GlobalSettingsView } from './GlobalSettingsView';

describe('全局设置', () => {
  it('loads and saves the Steam proxy', async () => {
    const saveProxy = vi.fn().mockResolvedValue({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    const saveKey = vi.fn();
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, updatedAt: '2026-08-19T09:00:00Z' })} saveProxy={saveProxy} saveKey={saveKey} />);

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
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} saveProxy={saveProxy} saveKey={saveKey} />);

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
});
