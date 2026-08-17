import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';
import { TargetServerTable } from './TargetServerTable';

const servers: TargetServer[] = [
  {
    id: 'a1',
    endpoint: '203.0.113.10:27015',
    requiresReservation: true,
    priority: 10,
    maxConcurrentWarmups: 2,
    attemptWindowSeconds: 180,
    playerTarget: 8,
    enabled: true,
    hasRconCredentials: true,
    createdAt: '2026-08-18T12:00:00Z',
    updatedAt: '2026-08-18T12:00:00Z',
  },
  {
    id: 'b2',
    endpoint: '203.0.113.11:27015',
    requiresReservation: false,
    priority: 0,
    maxConcurrentWarmups: 1,
    attemptWindowSeconds: 180,
    playerTarget: 4,
    enabled: false,
    hasRconCredentials: false,
    createdAt: '2026-08-18T12:00:00Z',
    updatedAt: '2026-08-18T12:00:00Z',
  },
  {
    id: 'c3',
    endpoint: '203.0.113.12:27015',
    requiresReservation: false,
    priority: 0,
    maxConcurrentWarmups: 1,
    attemptWindowSeconds: 180,
    playerTarget: 4,
    enabled: true,
    hasRconCredentials: false,
    createdAt: '2026-08-18T12:00:00Z',
    updatedAt: '2026-08-18T12:00:00Z',
  },
];

const observations: TargetServerObservation[] = [
  {
    targetServerId: 'a1',
    status: 'online',
    serverName: 'L4D2 HK Versus #1',
    playerCount: 2,
    maxPlayers: 12,
    observedAt: new Date(Date.now() - 12_000).toISOString(),
  },
  {
    targetServerId: 'b2',
    status: 'unavailable',
    serverName: null,
    playerCount: null,
    maxPlayers: null,
    observedAt: new Date(Date.now() - 20_000).toISOString(),
  },
  {
    targetServerId: 'c3',
    status: 'pending',
    serverName: null,
    playerCount: null,
    maxPlayers: null,
    observedAt: null,
  },
];

const warmups: WarmupStatus[] = [];

describe('TargetServerTable', () => {
  it('renders A2S live names and populations, while retaining explicit unavailable and pending states', () => {
    render(<TargetServerTable observations={observations} servers={servers} warmups={warmups} />);

    expect(screen.getByText('L4D2 HK Versus #1')).toBeInTheDocument();
    expect(screen.getByText('203.0.113.10:27015')).toBeInTheDocument();
    expect(screen.getByText('2 / 12')).toBeInTheDocument();
    expect(screen.getByText(/12 秒前/)).toBeInTheDocument();
    expect(screen.getAllByText('A2S 不可用').length).toBeGreaterThan(0);
    expect(screen.getAllByText('等待首次观测').length).toBeGreaterThan(0);
    expect(screen.getAllByText('-- / --')).toHaveLength(2);
  });

  it('keeps configuration available when the current A2S sample is unavailable', () => {
    const onOpenServer = vi.fn();
    render(<TargetServerTable observations={observations} onOpenServer={onOpenServer} servers={servers} warmups={warmups} />);

    fireEvent.click(screen.getByRole('button', { name: '配置 203.0.113.11:27015' }));

    expect(onOpenServer).toHaveBeenCalledWith(servers[1]);
  });
});
