import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TargetServerForm } from './TargetServerForm';

describe('目标服务器模式选择', () => {
  it('选择 coop 后提交预设模式值', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} />);

    const select = screen.getByLabelText('模式类型');
    expect(select).toHaveValue('');
    expect(screen.getByRole('option', { name: '未指定（默认 coop）' })).toBeInTheDocument();
    expect(screen.queryByRole('switch', { name: '需要大厅预留' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'versus' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'coop' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('服务器地址'), { target: { value: '203.0.113.7:27015' } });
    fireEvent.change(select, { target: { value: 'coop' } });
    fireEvent.submit(screen.getByRole('form', { name: '目标服务器配置表单' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ gameMode: 'coop', requiresReservation: false, playerTarget: 4 }));
  });

  it('未指定模式时提交 null', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText('服务器地址'), { target: { value: '203.0.113.7:27015' } });
    fireEvent.submit(screen.getByRole('form', { name: '目标服务器配置表单' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ gameMode: null }));
  });
});
