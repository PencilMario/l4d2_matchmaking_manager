import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TargetServersView } from './TargetServersView';
import { TargetServerModal } from './TargetServerModal';

describe('TargetServersView 设置弹窗', () => {
  it('reports the server settings modal lifecycle to the polling owner', () => {
    const onModalOpenChange = vi.fn();
    render(
      <TargetServersView
        attempts={[]}
        onCreateTarget={vi.fn()}
        onDeleteTarget={vi.fn()}
        onModalOpenChange={onModalOpenChange}
        onRefresh={vi.fn()}
        onToggleTargetEnabled={vi.fn()}
        onUpdateTarget={vi.fn()}
        targets={[]}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: '新增服务器' }));

    expect(onModalOpenChange).toHaveBeenCalledWith(true);
  });

  it('legacy target server priority input rejects negative values', () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(
      <TargetServerModal
        isOpen
        onClose={vi.fn()}
        onSubmit={onSubmit}
      />
    );

    const priority = screen.getByLabelText('调度优先级');
    expect(priority).toHaveAttribute('min', '0');
    expect(screen.getByText('整数，大于等于 0，数值越高越优先调度')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: /服务器地址/ }), { target: { value: '203.0.113.7:27015' } });
    fireEvent.change(priority, { target: { value: '-1' } });
    fireEvent.click(screen.getByRole('button', { name: '立即创建' }));

    expect(screen.getByText('调度优先级必须为大于等于 0 的整数')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
