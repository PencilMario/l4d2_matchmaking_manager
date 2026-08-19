import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { OverviewView } from './OverviewView';

describe('系统概览大厅 ID', () => {
  it('以普通文本显示大厅 ID', () => {
    render(
      <OverviewView
        targets={[]}
        agents={[]}
        observations={[]}
        attempts={[{
          id: 'attempt-1', targetServerId: 'server-1', targetServerEndpoint: '127.0.0.1:27015',
          agentId: 'agent-1', agentName: 'agent-1', operationMode: 'standard', status: 'active',
          phase: 'observing_state', lobbyId: '109775242226986793', remainingSeconds: 30,
          totalSeconds: 720, startedAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
        }]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    expect(screen.getByText('109775242226986793')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '复制大厅 ID 109775242226986793' })).not.toBeInTheDocument();
  });

  it('按目标服务器聚合并展示多个暖服节点', () => {
    render(
      <OverviewView
        targets={[{
          id: 'server-1',
          endpoint: '127.0.0.1:27015',
          name: '服务器[1]',
          currentPlayers: 0,
          maxPlayers: 12,
          a2sStatus: 'online',
          enabled: true,
          priority: 0,
          requiresReservation: false,
          maxConcurrentWarmups: 36,
          activeWarmupsCount: 2,
          attemptWindowSeconds: 720,
          playerTarget: 6,
          createdAt: '2026-08-19T00:00:00Z',
          updatedAt: '2026-08-19T00:00:00Z',
        }]}
        agents={[]}
        observations={[]}
        attempts={[
          {
            id: 'attempt-1',
            targetServerId: 'server-1',
            targetServerEndpoint: '127.0.0.1:27015',
            agentId: 'agent-1',
            agentName: '暖服节点-A',
            operationMode: 'standard',
            status: 'active',
            phase: 'awaiting_ready',
            lobbyId: '109775242226986793',
            remainingSeconds: 60,
            totalSeconds: 720,
            startedAt: '2026-08-19T00:00:00Z',
            updatedAt: '2026-08-19T00:00:00Z',
          },
          {
            id: 'attempt-2',
            targetServerId: 'server-1',
            targetServerEndpoint: '127.0.0.1:27015',
            agentId: 'agent-2',
            agentName: '暖服节点-B',
            operationMode: 'standard',
            status: 'active',
            phase: 'awaiting_ready',
            lobbyId: '109775242226986794',
            remainingSeconds: 30,
            totalSeconds: 720,
            startedAt: '2026-08-19T00:01:00Z',
            updatedAt: '2026-08-19T00:01:00Z',
          },
        ]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    expect(screen.getAllByText('服务器[1]')).toHaveLength(1);
    expect(screen.getByText('当前调度节点 2')).toBeInTheDocument();
    expect(screen.getByText('当前人数 0/12')).toBeInTheDocument();
    expect(screen.getByText('暖服节点[1]')).toBeInTheDocument();
    expect(screen.getByText('暖服节点[2]')).toBeInTheDocument();
    expect(screen.getByText('109775242226986793')).toBeInTheDocument();
    expect(screen.getByText('109775242226986794')).toBeInTheDocument();
  });
});
