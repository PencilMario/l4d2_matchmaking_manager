import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { STEAM_DOWNLOAD_REGIONS, WarmupAgentModal } from './WarmupAgentModal';

describe('WarmupAgentModal', () => {
  it('uses Steam regional download options and submits the Steam configuration value', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={onSubmit} />);

    expect(STEAM_DOWNLOAD_REGIONS).toHaveLength(187);
    expect(screen.getByRole('option', { name: '中国 - 香港' })).toHaveValue('hongkong');
    expect(screen.getByRole('option', { name: '中国 - 重庆' })).toHaveValue('chongqing');
    expect(screen.getByRole('option', { name: '日本 - 东京' })).toHaveValue('tokyo');

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'hk-agent' } });
    fireEvent.change(screen.getByLabelText('Steam 下载区域'), { target: { value: 'hongkong' } });
    fireEvent.click(screen.getByRole('button', { name: '创建节点' }));

    expect(onSubmit).toHaveBeenCalledWith({ name: 'hk-agent', steamRegion: 'hongkong', keepVncAlive: false });
  });

  it('sorts download regions by country and then city while keeping the default first', () => {
    const labels = STEAM_DOWNLOAD_REGIONS.map(region => region.label);

    expect(labels[0]).toBe('使用 Steam 默认区域');
    const chinaStart = labels.findIndex(label => label.startsWith('中国'));
    const chinaEnd = labels.length - 1 - [...labels].reverse().findIndex(label => label.startsWith('中国'));
    expect(chinaStart).toBeGreaterThan(0);
    expect(labels.slice(chinaStart, chinaEnd + 1).every(label => label.startsWith('中国'))).toBe(true);
    const sortedChineseLabels = [...labels.slice(chinaStart, chinaEnd + 1)].sort((left, right) =>
      new Intl.Collator('zh-CN').compare(left.split(' - ')[0], right.split(' - ')[0]) ||
      new Intl.Collator('zh-CN').compare(left.split(' - ')[1] ?? '', right.split(' - ')[1] ?? ''));
    expect(labels.slice(chinaStart, chinaEnd + 1)).toEqual(sortedChineseLabels);
  });

  it('prevents the mouse wheel from changing the focused Steam region', () => {
    render(<WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={vi.fn()} />);

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
      <WarmupAgentModal isOpen onClose={vi.fn()} onSubmit={vi.fn()} initialData={firstAgent} />
    );

    const regionSelect = screen.getByLabelText('Steam 下载区域');
    fireEvent.change(regionSelect, { target: { value: 'tokyo' } });

    rerender(
      <WarmupAgentModal
        isOpen
        onClose={vi.fn()}
        onSubmit={vi.fn()}
        initialData={{ ...firstAgent, updatedAt: '2026-08-19T00:00:05Z' }}
      />
    );

    expect(regionSelect).toHaveValue('tokyo');
  });
});
