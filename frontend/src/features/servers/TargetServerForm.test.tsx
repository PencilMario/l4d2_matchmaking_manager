import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TargetServerForm } from './TargetServerForm';

describe('目标服务器模式选择', () => {
  it('选择 coop 后提交预设模式值', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} />);

    const select = screen.getByLabelText('模式类型');
    expect(select).toHaveValue('');
    expect(screen.getByRole('option', { name: '未指定（保持默认）' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'versus' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'coop' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('服务器地址'), { target: { value: '203.0.113.7:27015' } });
    fireEvent.change(select, { target: { value: 'coop' } });
    fireEvent.submit(screen.getByRole('form', { name: '目标服务器配置表单' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ gameMode: 'coop' }));
  });

  it('未指定模式时提交 null', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText('服务器地址'), { target: { value: '203.0.113.7:27015' } });
    fireEvent.submit(screen.getByRole('form', { name: '目标服务器配置表单' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ gameMode: null }));
  });
});

describe('目标服务器调度优先级', () => {
  it('拒绝负数并允许 0', () => {
    const onSubmit = vi.fn();
    render(<TargetServerForm onSubmit={onSubmit} />);
    const form = screen.getByRole('form', { name: '目标服务器配置表单' });
    const priority = screen.getByRole('spinbutton', { name: '调度优先级' });

    expect(priority).toHaveAttribute('min', '0');
    fireEvent.change(screen.getByLabelText('服务器地址'), { target: { value: '203.0.113.7:27015' } });
    fireEvent.change(priority, { target: { value: '-1' } });
    fireEvent.submit(form);

    expect(screen.getByText('调度优先级必须为大于等于 0 的整数。')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();

    fireEvent.change(priority, { target: { value: '0' } });
    fireEvent.submit(form);

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ priority: 0 }));
  });
});
