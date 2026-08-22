import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiService, serializePlayerEntryStatisticsQuery, shanghaiLocalDateTimeToUtcIso } from '../services/api';
import type { PlayerEntryStatisticsResponse } from '../types';
import { Sidebar } from './Sidebar';
import { PlayerEntryStatisticsView } from './PlayerEntryStatisticsView';

const emptyDailyPattern = Array.from({ length: 24 }, (_, hour) => ({
  hour,
  label: `${String(hour).padStart(2, '0')}:00`,
  count: 0,
}));

const response: PlayerEntryStatisticsResponse = {
  fromUtc: '2026-08-22T16:00:00Z',
  toUtc: '2026-08-23T16:00:00Z',
  timeZone: 'Asia/Shanghai',
  granularity: 'hour',
  totalEntries: 18,
  entriesPerHour: 0.75,
  trend: [
    { bucketStartUtc: '2026-08-22T16:00:00Z', label: '08-23 00:00', count: 0 },
    { bucketStartUtc: '2026-08-22T17:00:00Z', label: '08-23 01:00', count: 6 },
  ],
  dailyPattern: emptyDailyPattern.map(item => item.hour === 1 ? { ...item, count: 6 } : item),
  agents: [
    {
      agentId: 'agent-1',
      agentName: '暖服节点 Alpha',
      nameSnapshots: ['暖服节点 Alpha'],
      entries: 6,
      entriesPerHour: 0.25,
      lastEntryAtUtc: '2026-08-23T15:40:00Z',
    },
  ],
  downloadRegions: [
    { key: null, label: '默认', entries: 4, entriesPerHour: 0.1667 },
    { key: '47', label: '上海', entries: 14, entriesPerHour: 0.5833 },
  ],
};

const query = {
  from: '2026-08-22T16:00:00.000Z',
  to: '2026-08-23T16:00:00.000Z',
  granularity: 'day' as const,
  lobbyType: 'reserved' as const,
  targetMode: 'coop' as const,
  agentId: 'agent-1',
  targetServerId: 'server-1',
};

const stateAgents = [
  {
    id: 'agent-1',
    name: '暖服节点 Alpha',
    status: 'running' as const,
    keepVncAlive: false,
    novncPort: 8083,
    activeAttemptsCount: 0,
    createdAt: '2026-08-20T00:00:00Z',
    updatedAt: '2026-08-20T00:00:00Z',
  },
];

const stateTargets = [
  {
    id: 'server-1',
    endpoint: '203.0.113.7:27015',
    name: '目标服务器 Alpha',
    currentPlayers: 0,
    maxPlayers: 12,
    a2sStatus: 'online' as const,
    enabled: true,
    priority: 0,
    requiresReservation: false,
    maxConcurrentWarmups: 2,
    activeWarmupsCount: 0,
    attemptWindowSeconds: 720,
    playerTarget: 6,
    createdAt: '2026-08-20T00:00:00Z',
    updatedAt: '2026-08-20T00:00:00Z',
  },
];

describe('玩家进入统计 API', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('serializes typed filters and omits unset dimensions', () => {
    const params = new URLSearchParams(serializePlayerEntryStatisticsQuery(query));

    expect(Object.fromEntries(params)).toEqual(query);
    expect(serializePlayerEntryStatisticsQuery({ granularity: 'auto' })).toBe('granularity=auto');
  });

  it('converts Shanghai datetime-local values to UTC without using browser timezone', () => {
    expect(shanghaiLocalDateTimeToUtcIso('2026-08-23T08:30')).toBe('2026-08-23T00:30:00.000Z');
    expect(shanghaiLocalDateTimeToUtcIso('2026-08-23T00:00')).toBe('2026-08-22T16:00:00.000Z');
    expect(shanghaiLocalDateTimeToUtcIso('2026-02-30T00:00')).toBeNull();
    expect(shanghaiLocalDateTimeToUtcIso('')).toBeNull();
  });

  it('calls the statistics endpoint with a type-safe query', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify(response), { status: 200, headers: { 'content-type': 'application/json' } }),
    );

    const result = await ApiService.fetchPlayerEntryStatistics(query);

    expect(result.data).toEqual(response);
    expect(fetchSpy).toHaveBeenCalledWith(
      `/v1/statistics/player-entries?${serializePlayerEntryStatisticsQuery(query)}`,
      expect.objectContaining({ method: 'GET' }),
    );
  });
});

describe('玩家进入统计页', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('shows filters, Shanghai mode guidance, charts, agent rows and default region rows', async () => {
    const fetchStatistics = vi.spyOn(ApiService, 'fetchPlayerEntryStatistics').mockResolvedValue({
      data: response,
      error: null,
      status: 200,
    });

    render(<PlayerEntryStatisticsView agents={stateAgents} targets={stateTargets} />);

    expect(await screen.findByRole('heading', { name: '玩家进入统计' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '最近 24 小时' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '最近 7 天' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '最近 30 天' })).toBeInTheDocument();
    expect(screen.getByLabelText('开始时间（上海）')).toBeInTheDocument();
    expect(screen.getByLabelText('结束时间（上海）')).toBeInTheDocument();
    expect(screen.getByLabelText('趋势粒度')).toBeInTheDocument();
    expect(screen.getByLabelText('大厅类型')).toBeInTheDocument();
    expect(screen.getByLabelText('目标模式')).toBeInTheDocument();
    expect(screen.getByLabelText('Agent 筛选')).toHaveTextContent('暖服节点 Alpha');
    expect(screen.getByLabelText('Target Server 筛选')).toHaveTextContent('目标服务器 Alpha');
    expect(screen.getByText(/未指定模式会归入 versus/)).toBeInTheDocument();
    expect(screen.getByText(/成功 ReplyJoinData 响应，不代表真实进服/)).toBeInTheDocument();

    expect(screen.getByText('18')).toBeInTheDocument();
    expect(screen.getByText('0.75')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '时间趋势' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: '进入趋势图' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '上海时间整日规律' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: '上海时间 0 到 23 点进入规律图' })).toBeInTheDocument();
    expect(within(screen.getByRole('table', { name: 'Agent 统计' })).getByText('暖服节点 Alpha')).toBeInTheDocument();
    expect(within(screen.getByRole('table', { name: '下载区域统计' })).getByText('默认')).toBeInTheDocument();
    expect(within(screen.getByRole('table', { name: '下载区域统计' })).getByText('上海')).toBeInTheDocument();
    expect(fetchStatistics).toHaveBeenCalledTimes(1);
  });

  it('uses an explicit Shanghai UTC range and selected dimensions after a custom query', async () => {
    const fetchStatistics = vi.spyOn(ApiService, 'fetchPlayerEntryStatistics').mockResolvedValue({
      data: response,
      error: null,
      status: 200,
    });

    render(<PlayerEntryStatisticsView agents={stateAgents} targets={stateTargets} />);
    await screen.findByRole('heading', { name: '玩家进入统计' });

    fireEvent.change(screen.getByLabelText('开始时间（上海）'), { target: { value: '2026-08-23T08:00' } });
    fireEvent.change(screen.getByLabelText('结束时间（上海）'), { target: { value: '2026-08-23T18:30' } });
    fireEvent.change(screen.getByLabelText('趋势粒度'), { target: { value: 'week' } });
    fireEvent.change(screen.getByLabelText('大厅类型'), { target: { value: 'reserved' } });
    fireEvent.change(screen.getByLabelText('目标模式'), { target: { value: 'versus' } });
    fireEvent.change(screen.getByLabelText('Agent 筛选'), { target: { value: 'agent-1' } });
    fireEvent.change(screen.getByLabelText('Target Server 筛选'), { target: { value: 'server-1' } });
    fireEvent.click(screen.getByRole('button', { name: '查询统计' }));

    await waitFor(() => expect(fetchStatistics).toHaveBeenCalledTimes(2));
    expect(fetchStatistics).toHaveBeenLastCalledWith({
      from: '2026-08-23T00:00:00.000Z',
      to: '2026-08-23T10:30:00.000Z',
      granularity: 'week',
      lobbyType: 'reserved',
      targetMode: 'versus',
      agentId: 'agent-1',
      targetServerId: 'server-1',
    });
  });

  it('shows a loading state, an empty state, and an error retry path', async () => {
    let resolveRequest: ((value: { data: PlayerEntryStatisticsResponse; error: null; status: number }) => void) | undefined;
    const pending = new Promise<{ data: PlayerEntryStatisticsResponse; error: null; status: number }>((resolve) => {
      resolveRequest = resolve;
    });
    const fetchStatistics = vi.spyOn(ApiService, 'fetchPlayerEntryStatistics')
      .mockReturnValueOnce(pending)
      .mockRejectedValueOnce(new Error('network down'))
      .mockResolvedValueOnce({
        data: { ...response, totalEntries: 0, entriesPerHour: 0, trend: [], agents: [], downloadRegions: [] },
        error: null,
        status: 200,
      });

    const { rerender } = render(<PlayerEntryStatisticsView agents={stateAgents} targets={stateTargets} />);
    expect(screen.getByText('正在读取玩家进入统计')).toBeInTheDocument();
    resolveRequest?.({ data: response, error: null, status: 200 });
    await screen.findByText('18');

    fireEvent.click(screen.getByRole('button', { name: '查询统计' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('读取玩家进入统计失败');
    fireEvent.click(screen.getByRole('button', { name: '重试' }));
    expect(await screen.findByText('选定范围内暂无进入事件')).toBeInTheDocument();
    expect(fetchStatistics).toHaveBeenCalledTimes(3);
    rerender(<PlayerEntryStatisticsView agents={stateAgents} targets={stateTargets} />);
  });
});

describe('统计导航入口', () => {
  it('adds the statistics tab to the mounted sidebar', () => {
    const onTabChange = vi.fn();
    render(
      <Sidebar
        activeTab="statistics"
        onTabChange={onTabChange}
        uncertainCount={0}
        unavailableCount={0}
        quarantinedCount={0}
      />,
    );

    const button = screen.getByRole('button', { name: '进入统计' });
    expect(button).toHaveAttribute('aria-current', 'page');
    fireEvent.click(button);
    expect(onTabChange).toHaveBeenCalledWith('statistics');
  });
});
