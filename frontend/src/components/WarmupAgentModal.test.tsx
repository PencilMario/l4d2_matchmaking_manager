import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { WarmupAgentModal } from './WarmupAgentModal';

const steamRegions = [
  { id: 32, name: '日本 - 东京' },
  { id: 33, name: '中国 - 香港' },
  { id: 47, name: '中国 - 上海' },
  { id: 168, name: '中国 - 青岛' },
  { id: 204, name: '中国 - 重庆' },
];

describe('WarmupAgentModal', () => {
  it('uses the Core regional directory and submits the internal region ID', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={onSubmit} steamRegions={steamRegions} />);

    expect(screen.getByRole('option', { name: '中国 - 香港' })).toHaveValue('33');
    expect(screen.getByRole('option', { name: '中国 - 重庆' })).toHaveValue('204');
    expect(screen.getByRole('option', { name: '日本 - 东京' })).toHaveValue('32');

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'hk-agent' } });
    fireEvent.change(screen.getByLabelText('Steam 下载区域'), { target: { value: '33' } });
    fireEvent.click(screen.getByRole('button', { name: '创建节点' }));

    expect(onSubmit).toHaveBeenCalledWith({ name: 'hk-agent', steamRegion: '33', keepVncAlive: false });
  });

  it('keeps the local default option before the fetched regions', () => {
    render(<WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={vi.fn()} steamRegions={steamRegions} />);

    const options = screen.getAllByRole('option');
    expect(options[0]).toHaveTextContent('使用 Steam 默认区域');
    expect(options.slice(1).map(option => option.textContent)).toEqual(steamRegions.map(region => region.name));
  });

  it('prevents the mouse wheel from changing the focused Steam region', () => {
    render(<WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={vi.fn()} steamRegions={steamRegions} />);

    const regionSelect = screen.getByLabelText('Steam 下载区域');
    regionSelect.focus();

    const wheelEvent = new WheelEvent('wheel', { bubbles: true, cancelable: true, deltaY: 100 });
    regionSelect.dispatchEvent(wheelEvent);

    expect(wheelEvent.defaultPrevented).toBe(true);
    expect(regionSelect).toHaveValue('');
  });

  it('keeps form values when polling replaces the same agent object', () => {
    const firstAgent = {
      id: 'agent-1',
      name: 'hk-agent',
      status: 'running' as const,
      steamRegion: 'hongkong',
      keepVncAlive: false,
      novncPort: 18083,
      activeAttemptsCount: 0,
      createdAt: '2026-08-19T00:00:00Z',
      updatedAt: '2026-08-19T00:00:00Z',
    };
    const { rerender } = render(
      <WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={vi.fn()} initialData={firstAgent} steamRegions={steamRegions} />
    );

    const regionSelect = screen.getByLabelText('Steam 下载区域');
    fireEvent.change(regionSelect, { target: { value: '32' } });

    rerender(
      <WarmupAgentModal
        isOpen
        onClose={vi.fn()}
        onSubmit={vi.fn()}
        initialData={{ ...firstAgent, updatedAt: '2026-08-19T00:00:05Z' }}
        steamRegions={steamRegions}
      />
    );

    expect(regionSelect).toHaveValue('32');
  });
});
