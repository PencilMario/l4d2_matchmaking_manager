import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AgentForm } from './AgentForm';

describe('AgentForm', () => {
  it('trims the name and sends a blank download region as null', () => {
    const onSubmit = vi.fn();
    render(<AgentForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText('Agent 名称'), { target: { value: ' hk-agent ' } });
    fireEvent.change(screen.getByLabelText('下载区域'), { target: { value: '  ' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Warm-up Agent 表单' }));

    expect(onSubmit).toHaveBeenCalledWith({ name: 'hk-agent', downloadRegion: null });
  });
});
