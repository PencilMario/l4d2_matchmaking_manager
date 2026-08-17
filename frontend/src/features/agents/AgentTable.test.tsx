import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { WarmupAgent, WarmupStatus } from '../../api/models';
import { AgentTable } from './AgentTable';

const agents: WarmupAgent[] = [
  { id: 'running', name: 'hk-running', status: 'running', downloadRegion: 'hongkong', noVncPort: 18083, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' },
  { id: 'stopped', name: 'hk-stopped', status: 'stopped', downloadRegion: null, noVncPort: 18084, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' },
  { id: 'quarantined', name: 'hk-quarantine', status: 'quarantined', downloadRegion: null, noVncPort: 18085, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' },
];
const warmups: WarmupStatus[] = [];

describe('AgentTable', () => {
  it('shows only status-appropriate actions and never offers automatic restart for quarantined agents', () => {
    const onAction = vi.fn();
    render(<AgentTable agents={agents} onAction={onAction} warmups={warmups} />);

    expect(screen.getByRole('button', { name: '停止 hk-running' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '启动 hk-stopped' })).toBeInTheDocument();
    expect(screen.getByText('需人工调查')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '启动 hk-quarantine' })).not.toBeInTheDocument();
    expect(screen.getByText('仅本机 :18083')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '停止 hk-running' }));
    expect(onAction).toHaveBeenCalledWith('stop', agents[0]);
  });
});
