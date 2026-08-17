import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';
import { CoreApiError } from '../../api/core-client';
import { TargetServerDrawer } from './TargetServerDrawer';

const server: TargetServer = {
  id: 'target-1', endpoint: '203.0.113.10:27015', requiresReservation: true, priority: 5,
  maxConcurrentWarmups: 1, attemptWindowSeconds: 180, playerTarget: 8, enabled: true,
  hasRconCredentials: true, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z',
};
const observation: TargetServerObservation = {
  targetServerId: 'target-1', status: 'online', serverName: 'L4D2 HK Versus #1', playerCount: 2, maxPlayers: 12, observedAt: '2026-08-18T12:00:00Z',
};
const warmups: WarmupStatus[] = [{
  targetServerId: 'target-1', targetEndpoint: server.endpoint, warmupAgentId: 'agent-1', warmupAgentName: 'HK Agent', operationId: 'operation-1',
  lobbyId: '123', mode: 'versus', state: 'active', phase: 'waiting', startedAt: '2026-08-18T12:00:00Z', lobbyReadyAt: null,
  firstExternalMemberAt: null, quietSince: null, observedAt: '2026-08-18T12:00:00Z', deadline: '2026-08-18T12:03:00Z', remainingSeconds: 120,
}];

describe('TargetServerDrawer', () => {
  it('separates current A2S fields from configuration and associated warmups', () => {
    render(<TargetServerDrawer observation={observation} onClose={vi.fn()} server={server} warmups={warmups} />);

    expect(screen.getByRole('dialog', { name: 'L4D2 HK Versus #1 配置' })).toBeInTheDocument();
    expect(screen.getByText('2 / 12')).toBeInTheDocument();
    expect(screen.getByText('203.0.113.10:27015')).toBeInTheDocument();
    expect(screen.getByText('预留大厅')).toBeInTheDocument();
    expect(screen.getByText('关联暖服 1')).toBeInTheDocument();
  });

  it('closes without changing configuration', () => {
    const onClose = vi.fn();
    render(<TargetServerDrawer observation={observation} onClose={onClose} server={server} warmups={warmups} />);

    fireEvent.click(screen.getByRole('button', { name: '关闭配置抽屉' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('keeps the form available after a failed disable drain and refreshes the snapshot', async () => {
    const onUpdate = vi.fn().mockRejectedValue(new CoreApiError(409, 'target_server_drain_failed'));
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    render(<TargetServerDrawer observation={observation} onClose={vi.fn()} onRefresh={onRefresh} onUpdate={onUpdate} server={server} warmups={warmups} />);

    fireEvent.click(screen.getByRole('button', { name: '编辑配置' }));
    fireEvent.click(screen.getByLabelText('启用调度'));
    fireEvent.submit(screen.getByRole('form', { name: 'Target Server 配置表单' }));
    fireEvent.click(screen.getByRole('button', { name: '继续' }));
    fireEvent.click(screen.getByRole('button', { name: '确认禁用' }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledWith(expect.objectContaining({ enabled: false })));
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));
    expect(screen.getByRole('form', { name: 'Target Server 配置表单' })).toBeInTheDocument();
    expect(screen.getByText('target_server_drain_failed')).toBeInTheDocument();
  });
});
