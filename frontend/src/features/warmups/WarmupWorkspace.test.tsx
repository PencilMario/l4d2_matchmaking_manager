import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';
import { WarmupWorkspace } from './WarmupWorkspace';

const servers: TargetServer[] = [{
  id: 'target-1', endpoint: '198.51.100.10:27015', requiresReservation: true, priority: 3,
  maxConcurrentWarmups: 2, attemptWindowSeconds: 180, playerTarget: 8, enabled: true,
  hasRconCredentials: true, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z',
}];

const observations: TargetServerObservation[] = [{
  targetServerId: 'target-1', status: 'unavailable', serverName: null, playerCount: null, maxPlayers: null, observedAt: null,
}];

const warmups: WarmupStatus[] = [{
  targetServerId: 'target-1', targetEndpoint: '198.51.100.10:27015', warmupAgentId: 'agent-1', warmupAgentName: 'HK Agent',
  operationId: 'operation-1', lobbyId: null, mode: 'versus', state: 'uncertain', phase: 'awaiting_external_members',
  startedAt: '2026-08-18T12:00:00Z', lobbyReadyAt: null, firstExternalMemberAt: null, quietSince: null,
  observedAt: '2026-08-18T12:01:00Z', deadline: '2026-08-18T12:04:00Z', remainingSeconds: 173,
}];

describe('WarmupWorkspace', () => {
  it('surfaces active, uncertain, and unavailable counts while preserving phase and missing lobby state', () => {
    render(<WarmupWorkspace observations={observations} servers={servers} warmups={warmups} />);

    expect(screen.getByText('活动暖服')).toBeInTheDocument();
    expect(screen.getAllByText('1').length).toBeGreaterThanOrEqual(3);
    expect(screen.getAllByText('操作未确认').length).toBeGreaterThan(0);
    expect(screen.getByText('awaiting_external_members')).toBeInTheDocument();
    expect(screen.getByText('尚未返回')).toBeInTheDocument();
    expect(screen.getByText('173 秒')).toBeInTheDocument();
  });

  it('uses the topology units only to select a Target Server', () => {
    const onSelectTarget = vi.fn();
    render(<WarmupWorkspace observations={observations} onSelectTarget={onSelectTarget} servers={servers} warmups={warmups} />);

    fireEvent.click(screen.getByRole('button', { name: '筛选 198.51.100.10:27015' }));

    expect(onSelectTarget).toHaveBeenCalledWith('target-1');
  });
});
