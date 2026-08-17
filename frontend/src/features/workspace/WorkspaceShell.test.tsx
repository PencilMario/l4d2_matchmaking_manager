import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { WorkspaceShell } from './WorkspaceShell';

const client = {
  listServers: vi.fn().mockResolvedValue([]),
  listAgents: vi.fn().mockResolvedValue([]),
  listWarmups: vi.fn().mockResolvedValue([]),
  listServerObservations: vi.fn().mockResolvedValue([]),
};

describe('WorkspaceShell', () => {
  it('renders status, desktop navigation, and a visual layer behind data', async () => {
    render(<WorkspaceShell client={client} />);

    expect(screen.getByText('Core online')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '当前暖服' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Target Server' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Warm-up Agent' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lobby 查询' })).toBeInTheDocument();
    expect(document.querySelector('[data-visual-stage]')).toBeInTheDocument();
    expect(document.querySelector('[data-workspace-data]')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(/LAST SYNC \d/)).toBeInTheDocument());
  });

  it('renders the Warm-up queue and opens a filtered A2S resource table from its topology selection', async () => {
    client.listServers.mockResolvedValueOnce([{
      id: 'target-1', endpoint: '198.51.100.10:27015', requiresReservation: false, priority: 1,
      maxConcurrentWarmups: 1, attemptWindowSeconds: 180, playerTarget: 4, enabled: true,
      hasRconCredentials: false, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z',
    }]);
    client.listWarmups.mockResolvedValueOnce([{
      targetServerId: 'target-1', targetEndpoint: '198.51.100.10:27015', warmupAgentId: 'agent-1', warmupAgentName: 'HK Agent',
      operationId: 'op-1', lobbyId: null, mode: 'versus', state: 'active', phase: 'awaiting_members', startedAt: '2026-08-18T12:00:00Z',
      lobbyReadyAt: null, firstExternalMemberAt: null, quietSince: null, observedAt: '2026-08-18T12:00:00Z', deadline: '2026-08-18T12:03:00Z', remainingSeconds: 120,
    }]);
    client.listServerObservations.mockResolvedValueOnce([{
      targetServerId: 'target-1', status: 'online', serverName: 'L4D2 HK Versus #1', playerCount: 2, maxPlayers: 12, observedAt: new Date().toISOString(),
    }]);

    render(<WorkspaceShell client={client} />);

    await screen.findByText('awaiting_members');
    fireEvent.click(screen.getByRole('button', { name: '筛选 198.51.100.10:27015' }));

    expect(await screen.findByText('L4D2 HK Versus #1')).toBeInTheDocument();
    expect(screen.getByText('2 / 12')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '配置 198.51.100.10:27015' }));
    expect(screen.getByRole('dialog', { name: 'L4D2 HK Versus #1 配置' })).toBeInTheDocument();
  });
});
