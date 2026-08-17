import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CoreApiError } from '../../api/core-client';
import type { LobbySnapshot } from '../../api/models';
import { LobbyQueryView } from './LobbyQueryView';

const completeLobby: LobbySnapshot = {
  lobbyId: '109775242425650097', ownerSteamId: '76561198000000001', observedAt: '2026-08-18T12:00:00Z', memberDataStatus: 'complete',
  members: [{ steamId: '76561198000000002', personaName: 'Player One' }], metadata: { 'Game:state': 'game', 'Members:numPlayers': '99' },
};

describe('LobbyQueryView', () => {
  it('rejects zero before requesting Core and renders confirmed members for complete responses', async () => {
    const queryLobby = vi.fn().mockResolvedValue(completeLobby);
    render(<LobbyQueryView queryLobby={queryLobby} />);

    fireEvent.change(screen.getByLabelText('Lobby ID'), { target: { value: '0' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Lobby 查询表单' }));
    expect(screen.getByText('请输入非零十进制 Lobby ID。')).toBeInTheDocument();
    expect(queryLobby).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('Lobby ID'), { target: { value: completeLobby.lobbyId } });
    fireEvent.submit(screen.getByRole('form', { name: 'Lobby 查询表单' }));
    expect(await screen.findByText('Player One')).toBeInTheDocument();
    expect(screen.getByText('确认成员 1')).toBeInTheDocument();
    expect(screen.getByText('99')).toBeInTheDocument();
  });

  it('does not infer a member count from metadata-only snapshots', async () => {
    const queryLobby = vi.fn().mockResolvedValue({ ...completeLobby, memberDataStatus: 'metadata_only_join_timeout', members: [] });
    render(<LobbyQueryView queryLobby={queryLobby} />);

    fireEvent.change(screen.getByLabelText('Lobby ID'), { target: { value: completeLobby.lobbyId } });
    fireEvent.submit(screen.getByRole('form', { name: 'Lobby 查询表单' }));

    expect(await screen.findByText('成员数据未确认')).toBeInTheDocument();
    expect(screen.getByText('成员数 不可用')).toBeInTheDocument();
    expect(screen.queryByText('确认成员 0')).not.toBeInTheDocument();
  });

  it('marks the previous result stale when a later 503 query fails', async () => {
    const queryLobby = vi.fn().mockResolvedValueOnce(completeLobby).mockRejectedValueOnce(new CoreApiError(503, 'lobby_query_agent_unavailable'));
    render(<LobbyQueryView queryLobby={queryLobby} />);

    fireEvent.change(screen.getByLabelText('Lobby ID'), { target: { value: completeLobby.lobbyId } });
    fireEvent.submit(screen.getByRole('form', { name: 'Lobby 查询表单' }));
    await screen.findByText('Player One');
    fireEvent.submit(screen.getByRole('form', { name: 'Lobby 查询表单' }));

    await waitFor(() => expect(screen.getByText('查询 Agent 当前不可用；上次结果已过期。')).toBeInTheDocument());
    expect(screen.getByText('Player One')).toBeInTheDocument();
  });
});
