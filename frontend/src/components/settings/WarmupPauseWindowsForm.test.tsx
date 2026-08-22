import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { WarmupPauseWindowsSettings } from '../../api/models';
import { WarmupPauseWindowsForm } from './WarmupPauseWindowsForm';

const savedSettings: WarmupPauseWindowsSettings = {
  windows: [],
  active: false,
  updatedAt: '2026-08-23T10:00:00Z',
};

describe('暖服暂停时间段表单', () => {
  it('keeps an unsaved window when the parent rerenders with unchanged settings', () => {
    const onSave = vi.fn().mockResolvedValue(savedSettings);
    const { rerender } = render(
      <WarmupPauseWindowsForm initialActive={false} initialWindows={[]} onSave={onSave} />,
    );

    fireEvent.click(screen.getByRole('button', { name: '新增时间段' }));
    expect(screen.getByLabelText('开始时间 1')).toBeInTheDocument();

    rerender(
      <WarmupPauseWindowsForm initialActive={false} initialWindows={[]} onSave={onSave} />,
    );

    expect(screen.getByLabelText('开始时间 1')).toBeInTheDocument();
  });
});
