import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SettingsWorkspace } from './SettingsWorkspace';

describe('全局设置', () => {
  it('does not reveal a configured Steam Web API key and can replace it', async () => {
    const update = vi.fn().mockResolvedValue({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T10:00:00Z' });
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} updateSettings={update} />);

    const input = await screen.findByLabelText('Steam Web API Key');
    expect(input).toHaveAttribute('type', 'password');
    expect(input).toHaveValue('');
    expect(screen.getByText('当前状态：已配置')).toBeInTheDocument();
    fireEvent.change(input, { target: { value: 'new-secret-key' } });
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }));

    await waitFor(() => expect(update).toHaveBeenCalledWith({ steamProxyUrl: null, steamWebApiKey: 'new-secret-key' }));
  });

  it('clears the Steam Web API key with an explicit action', async () => {
    const update = vi.fn().mockResolvedValue({ steamProxyUrl: null, steamWebApiKeyConfigured: false, updatedAt: '2026-08-19T10:00:00Z' });
    render(<SettingsWorkspace getSettings={() => Promise.resolve({ steamProxyUrl: null, steamWebApiKeyConfigured: true, updatedAt: '2026-08-19T09:00:00Z' })} updateSettings={update} />);

    await screen.findByLabelText('Steam Web API Key');
    fireEvent.click(screen.getByRole('button', { name: '清除 Steam Web API Key' }));

    await waitFor(() => expect(update).toHaveBeenCalledWith({ steamProxyUrl: null, clearSteamWebApiKey: true }));
  });
});
