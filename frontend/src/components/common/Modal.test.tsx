import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Modal, shouldCloseFromBackdrop } from './Modal';

describe('Modal 遮罩', () => {
  it('不会因对话框内部事件冒泡而关闭', () => {
    const onClose = vi.fn();

    render(
      <Modal isOpen onClose={onClose} title="编辑节点">
        <select aria-label="Steam 下载区域">
          <option>中国 - 香港</option>
        </select>
      </Modal>
    );

    const dialog = screen.getByRole('dialog');
    const backdrop = dialog.parentElement?.querySelector('[aria-hidden="true"]');
    expect(backdrop).toBeTruthy();
    fireEvent.wheel(screen.getByLabelText('Steam 下载区域'));

    expect(onClose).not.toHaveBeenCalled();
    expect(shouldCloseFromBackdrop({ target: dialog, currentTarget: backdrop } as never)).toBe(false);
    expect(shouldCloseFromBackdrop({ target: backdrop, currentTarget: backdrop } as never)).toBe(true);
  });
});
