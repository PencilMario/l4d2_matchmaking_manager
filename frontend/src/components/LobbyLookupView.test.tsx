import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { LobbyLookupView } from './LobbyLookupView';
import { ApiService } from '../services/api';

describe('大厅查询加入 URI', () => {
  it('不预填大厅 ID 或显示示例，输入后查询成功显示可复制的 Steam 加入 URI', async () => {
    vi.spyOn(ApiService, 'lookupLobby').mockResolvedValue({
      data: {
        lobbyId: '109775242226986793',
        ownerSteamId: '76561199012457364',
        memberStatus: 'complete',
        confirmedMemberCount: 1,
        observedAt: '2026-08-19T00:00:00Z',
        members: [],
        metadata: {},
      },
      error: null,
      status: 200,
    });

    render(<LobbyLookupView />);
    const lobbyIdInput = screen.getByPlaceholderText('请输入大厅 ID (64 位非零十进制数字)');
    expect(lobbyIdInput).toHaveValue('');
    expect(screen.getByRole('button', { name: '查询大厅' })).toBeDisabled();
    expect(screen.queryByText('示例大厅 ID：')).not.toBeInTheDocument();

    fireEvent.change(lobbyIdInput, { target: { value: '109775242226986793' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));

    const uri = await screen.findByDisplayValue(
      'steam://joinlobby/550/109775242226986793/76561199012457364'
    );
    expect(uri).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '复制加入 URI' })).toBeInTheDocument();
  });

  it('显示 Steam Web API 返回的头像并为缺失头像保留默认图标', async () => {
    vi.spyOn(ApiService, 'lookupLobby').mockResolvedValue({
      data: { lobbyId: '109775242226986793', ownerSteamId: '76561199012457364', memberStatus: 'complete', confirmedMemberCount: 2, observedAt: '2026-08-19T00:00:00Z', members: [{ steamId: '76561199012457364', personaName: 'Player One', avatarUrl: 'https://cdn.example/player.jpg', isReady: false, joinedAt: '2026-08-19T00:00:00Z' }, { steamId: '76561199012457365', personaName: '', avatarUrl: null, isReady: false, joinedAt: '2026-08-19T00:00:00Z' }], metadata: {} },
      error: null,
      status: 200,
    });

    render(<LobbyLookupView />);
    fireEvent.change(screen.getByPlaceholderText('请输入大厅 ID (64 位非零十进制数字)'), { target: { value: '109775242226986793' } });
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));

    expect(await screen.findByAltText('Player One 头像')).toHaveAttribute('src', 'https://cdn.example/player.jpg');
    expect(screen.getByLabelText('未知玩家头像')).toBeInTheDocument();
  });
});
