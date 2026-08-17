import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { WarmupAgent, WarmupStatus } from '../../api/models';
import { AgentWorkspace } from './AgentWorkspace';

const agent: WarmupAgent = { id: 'running', name: 'hk-running', status: 'running', downloadRegion: null, noVncPort: 18083, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' };

describe('AgentWorkspace', () => {
  it('requires a Stepper confirmation before recreating an Agent and refreshes the list afterwards', async () => {
    const onAction = vi.fn().mockResolvedValue(undefined);
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    render(<AgentWorkspace agents={[agent]} onAction={onAction} onRefresh={onRefresh} warmups={[] as WarmupStatus[]} />);

    fireEvent.click(screen.getByRole('button', { name: '重建 hk-running' }));
    expect(screen.getByText(/保留 Steam 登录与账号配置卷/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '继续' }));
    fireEvent.click(screen.getByRole('button', { name: '确认重建' }));

    await waitFor(() => expect(onAction).toHaveBeenCalledWith('recreate', agent));
    await waitFor(() => expect(onRefresh).toHaveBeenCalledTimes(1));
  });
});
