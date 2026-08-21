import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TargetServersView } from './TargetServersView';

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
});
