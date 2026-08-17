import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { TargetServer } from '../../api/models';
import { TargetServerForm } from './TargetServerForm';

const server: TargetServer = {
  id: 'target-1', endpoint: '203.0.113.10:27015', requiresReservation: true, priority: 5,
  maxConcurrentWarmups: 1, attemptWindowSeconds: 180, playerTarget: 8, enabled: true,
  hasRconCredentials: true, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z',
};

describe('TargetServerForm', () => {
  it('submits the complete PUT body, representing cleared optional numeric fields as null', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} server={server} />);

    expect(screen.getByLabelText('RCON 密码')).toHaveValue('');
    fireEvent.change(screen.getByLabelText('endpoint'), { target: { value: '203.0.113.50:27015' } });
    fireEvent.change(screen.getByLabelText('priority'), { target: { value: '' } });
    fireEvent.click(screen.getByLabelText('启用调度'));
    fireEvent.change(screen.getByLabelText('RCON 密码'), { target: { value: 'write-only-secret' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Target Server 配置表单' }));

    expect(onSubmit).toHaveBeenCalledWith({
      endpoint: '203.0.113.50:27015',
      requiresReservation: true,
      priority: null,
      maxConcurrentWarmups: 1,
      attemptWindowSeconds: 180,
      playerTarget: 8,
      enabled: false,
      rconPassword: 'write-only-secret',
    });
  });

  it('hides the RCON writer when reservation is disabled and clears its outgoing value', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} server={server} />);

    fireEvent.click(screen.getByLabelText('预留大厅'));
    expect(screen.queryByLabelText('RCON 密码')).not.toBeInTheDocument();
    fireEvent.submit(screen.getByRole('form', { name: 'Target Server 配置表单' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ requiresReservation: false, rconPassword: null }));
  });
});
