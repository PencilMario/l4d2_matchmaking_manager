import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GlobalSettingsView } from './GlobalSettingsView';

describe('全局设置', () => {
  it('loads and saves the Steam proxy', async () => {
    const save = vi.fn().mockResolvedValue({ steamProxyUrl: 'http://127.0.0.1:7890/', steamWebApiKeyConfigured: false, updatedAt: '2026-08-19T10:00:00Z' });
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: false, updatedAt: '2026-08-19T09:00:00Z' })} save={save} />);

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }));

    await waitFor(() => expect(save).toHaveBeenCalledWith({ steamProxyUrl: 'http://127.0.0.1:7890' }));
    expect(await screen.findByText('全局设置已保存。')).toBeInTheDocument();
  });

  it('does not reveal, replaces, and clears the Steam Web API key', async () => {
    const save = vi.fn().mockResolvedValue({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T10:00:00Z' });
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} save={save} />);

    const key = await screen.findByLabelText('Steam Web API Key');
    expect(key).toHaveAttribute('type', 'password');
    expect(key).toHaveValue('');
    fireEvent.change(key, { target: { value: 'replacement-key' } });
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith({ steamProxyUrl: null, steamWebApiKey: 'replacement-key' }));

    fireEvent.click(screen.getByRole('button', { name: '清除 Steam Web API Key' }));
    await waitFor(() => expect(save).toHaveBeenLastCalledWith({ steamProxyUrl: null, clearSteamWebApiKey: true }));
  });
});
