import { fireEvent, render, screen, waitFor } from '@testing-library/react'; import { describe, expect, it, vi } from 'vitest'; import { LobbyQueryView } from './LobbyQueryView';

const snapshot = { lobbyId: '109775242646351561', ownerSteamId: null, members: [], metadata: { x: 'y' }, observedAt: '2026-08-18T12:00:00Z', memberDataStatus: 'partial' };

describe('大厅查询', () => {
  it('将无前缀十六进制大厅 ID 转为十进制后查询', async () => {
    const query = vi.fn().mockResolvedValue(snapshot);
    render(<LobbyQueryView queryLobby={query} />);
    fireEvent.change(screen.getByLabelText('大厅 ID'), { target: { value: '186000067116ec9' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));
    await screen.findByText('查询结果');
    expect(query).toHaveBeenCalledWith('109775242646351561');
  });

  it('保留 18 位十进制大厅 ID，并将短纯数字按十六进制处理', async () => {
    const query = vi.fn().mockResolvedValue(snapshot);
    render(<LobbyQueryView queryLobby={query} />);
    const input = screen.getByLabelText('大厅 ID');
    fireEvent.change(input, { target: { value: '109775242646351561' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));
    await screen.findByText('查询结果');
    expect(query).toHaveBeenCalledWith('109775242646351561');

    fireEvent.change(input, { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));
    await waitFor(() => expect(query).toHaveBeenLastCalledWith('2'));
  });

  it('拒绝零值和非法大厅 ID', () => {
    const query = vi.fn();
    render(<LobbyQueryView queryLobby={query} />);
    const input = screen.getByLabelText('大厅 ID');
    fireEvent.change(input, { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));
    expect(screen.getByText('请输入非零十六进制或 18 位十进制大厅 ID。')).toBeInTheDocument();

    fireEvent.change(input, { target: { value: 'not-an-id' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));
    expect(query).not.toHaveBeenCalled();
  });
});
