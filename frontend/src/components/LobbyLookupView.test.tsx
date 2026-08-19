import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { LobbyLookupView } from './LobbyLookupView';
import { ApiService } from '../services/api';

describe('大厅查询加入 URI', () => {
  it('查询成功后显示可复制的 Steam 加入 URI', async () => {
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
    fireEvent.click(screen.getByRole('button', { name: '查询大厅' }));

    const uri = await screen.findByDisplayValue(
      'steam://joinlobby/550/109775242226986793/76561199012457364'
    );
    expect(uri).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '复制加入 URI' })).toBeInTheDocument();
  });
});
