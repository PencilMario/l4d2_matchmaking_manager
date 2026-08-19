import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GlobalSettingsView } from './GlobalSettingsView';

describe('全局设置', () => {
  it('loads and saves the Steam proxy', async () => {
    const save = vi.fn().mockResolvedValue({ steamProxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-19T10:00:00Z' });
    render(<GlobalSettingsView load={() => Promise.resolve({ steamProxyUrl: null, updatedAt: '2026-08-19T09:00:00Z' })} save={save} />);

    const input = await screen.findByLabelText('Steam 代理地址');
    fireEvent.change(input, { target: { value: 'http://127.0.0.1:7890' } });
    fireEvent.click(screen.getByRole('button', { name: '保存设置' }));

    await waitFor(() => expect(save).toHaveBeenCalledWith('http://127.0.0.1:7890'));
    expect(await screen.findByText('全局设置已保存。重建已启用 VNC 的暖服节点后生效。')).toBeInTheDocument();
  });
});
