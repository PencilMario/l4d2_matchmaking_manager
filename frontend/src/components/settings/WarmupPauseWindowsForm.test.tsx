import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { WarmupPauseWindowsSettings } from '../../api/models';
import { WarmupPauseWindowsForm } from './WarmupPauseWindowsForm';

const savedSettings: WarmupPauseWindowsSettings = {
  windows: [],
  active: false,
  updatedAt: '2026-08-23T10:00:00Z',
};

describe('暖服暂停时间段表单', () => {
  it('syncs changed external settings while the draft is clean', () => {
    const onSave = vi.fn().mockResolvedValue(savedSettings);
    const { rerender } = render(
      <WarmupPauseWindowsForm initialActive={false} initialWindows={[]} onSave={onSave} />,
    );

    rerender(
      <WarmupPauseWindowsForm
        initialActive
        initialWindows={[{ start: '00:00', end: '08:00' }]}
        onSave={onSave}
      />,
    );

    expect(screen.getByLabelText('开始时间 1')).toHaveValue('00:00');
    expect(screen.getByText('当前状态：正在暂停')).toBeInTheDocument();
  });

  it('keeps an unsaved window when the parent rerenders with changed settings', () => {
    const onSave = vi.fn().mockResolvedValue(savedSettings);
    const { rerender } = render(
      <WarmupPauseWindowsForm initialActive={false} initialWindows={[]} onSave={onSave} />,
    );

    fireEvent.click(screen.getByRole('button', { name: '新增时间段' }));
    fireEvent.change(screen.getByLabelText('开始时间 1'), { target: { value: '23:00' } });
    expect(screen.getByLabelText('开始时间 1')).toBeInTheDocument();

    rerender(
      <WarmupPauseWindowsForm
        initialActive
        initialWindows={[{ start: '08:00', end: '12:00' }]}
        onSave={onSave}
      />,
    );

    expect(screen.getByLabelText('开始时间 1')).toHaveValue('23:00');
    expect(screen.getByText('当前状态：未暂停')).toBeInTheDocument();
  });

  it('disables window inputs while saving', async () => {
    let resolveSave!: (value: WarmupPauseWindowsSettings) => void;
    const onSave = vi.fn(() => new Promise<WarmupPauseWindowsSettings>(resolve => {
      resolveSave = resolve;
    }));
    render(
      <WarmupPauseWindowsForm
        initialActive={false}
        initialWindows={[{ start: '00:00', end: '08:00' }]}
        onSave={onSave}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: '保存暖服暂停时间段' }));
    expect(screen.getByLabelText('开始时间 1')).toBeDisabled();

    resolveSave({
      ...savedSettings,
      windows: [{ start: '00:00', end: '08:00' }],
    });
    await waitFor(() => expect(screen.getByLabelText('开始时间 1')).not.toBeDisabled());
  });
});
