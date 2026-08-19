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
});
