import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { WarmupAgentsView } from './WarmupAgentsView';

const agent = {
  id: 'agent-1',
  name: 'hk-agent',
  status: 'running' as const,
  steamRegion: undefined,
  keepVncAlive: false,
  novncPort: 18083,
  activeAttemptsCount: 0,
  createdAt: '2026-08-19T00:00:00Z',
  updatedAt: '2026-08-19T00:00:00Z',
};

describe('WarmupAgentsView VNC access', () => {
  it('keeps the VNC connection action disabled until the node enables its VNC service', () => {
    render(
      <WarmupAgentsView
        agents={[agent]}
        attempts={[]}
        onCreateAgent={vi.fn()}
        onDeleteAgent={vi.fn()}
        onRebuildAgent={vi.fn()}
        onRefresh={vi.fn()}
        onStartAgent={vi.fn()}
        onStopAgent={vi.fn()}
        onUpdateAgent={vi.fn()}
      />
    );

    expect(screen.getByRole('button', { name: '打开 VNC 连接 hk-agent' })).toBeDisabled();
  });

  it('shows a yellow login-pending status while a running node is not dispatchable', () => {
    render(
      <WarmupAgentsView
        agents={[{ ...agent, keepVncAlive: true, ready: false }]}
        attempts={[]}
        onCreateAgent={vi.fn()}
        onDeleteAgent={vi.fn()}
        onRebuildAgent={vi.fn()}
        onRefresh={vi.fn()}
        onStartAgent={vi.fn()}
        onStopAgent={vi.fn()}
        onUpdateAgent={vi.fn()}
      />
    );

    expect(screen.getByText('等待 Steam 登录').parentElement).toHaveClass('bg-amber-50');
  });
});
