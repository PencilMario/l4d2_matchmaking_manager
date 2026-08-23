import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertCircle, BarChart3, Loader2, RefreshCw, Search } from 'lucide-react';
import type {
  PlayerEntryStatisticsGranularity,
  PlayerEntryStatisticsQuery,
  PlayerEntryStatisticsResponse,
  PlayerEntryStatisticsTargetMode,
  PlayerEntryStatisticsLobbyType,
  TargetServer,
  WarmupAgent,
} from '../types';
import { ApiService, formatShanghaiDateTimeInput, shanghaiLocalDateTimeToUtcIso } from '../services/api';
import './player-entry-statistics.css';

interface PlayerEntryStatisticsViewProps {
  agents: readonly WarmupAgent[];
  targets: readonly TargetServer[];
}

type RangePreset = 24 | 168 | 720;

const TIME_ZONE = 'Asia/Shanghai';

function createInitialRange() {
  const to = new Date();
  const from = new Date(to.getTime() - (24 * 60 * 60 * 1000));
  return {
    from: formatShanghaiDateTimeInput(from),
    to: formatShanghaiDateTimeInput(to),
  };
}

function toQueryValue(value: string): string | undefined {
  return value || undefined;
}

function formatCount(value: number): string {
  return new Intl.NumberFormat('zh-CN').format(value);
}

function formatRate(value: number): string {
  if (!Number.isFinite(value)) return '0';
  return value.toFixed(2).replace(/\.00$/, '').replace(/(\.\d)0$/, '$1');
}

function formatShanghaiDateTime(value: string | null | undefined): string {
  if (!value) return '--';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '--';
  return formatShanghaiDateTimeInput(date).replace('T', ' ');
}

function queryFromInputs(
  from: string,
  to: string,
  granularity: PlayerEntryStatisticsGranularity,
  lobbyType: PlayerEntryStatisticsLobbyType,
  targetMode: PlayerEntryStatisticsTargetMode,
  agentId: string,
  targetServerId: string,
): PlayerEntryStatisticsQuery | null {
  const fromUtc = shanghaiLocalDateTimeToUtcIso(from);
  const toUtc = shanghaiLocalDateTimeToUtcIso(to);
  if (!fromUtc || !toUtc) return null;
  return {
    from: fromUtc,
    to: toUtc,
    granularity,
    lobbyType,
    targetMode,
    agentId: toQueryValue(agentId),
    targetServerId: toQueryValue(targetServerId),
  };
}

function TrendChart({
  points,
  daily,
  ariaLabel,
}: {
  points: ReadonlyArray<{ label: string; count: number }>;
  daily?: boolean;
  ariaLabel: string;
}) {
  const width = 760;
  const height = 220;
  const chartTop = 18;
  const chartBottom = 178;
  const chartHeight = chartBottom - chartTop;
  const max = Math.max(1, ...points.map((point) => point.count));
  const step = points.length > 32 ? Math.ceil(points.length / 16) : 1;
  const barWidth = Math.max(3, Math.min(30, (width - 42) / Math.max(points.length, 1) - 4));
  const usableWidth = width - 42;

  return (
    <svg
      aria-label={ariaLabel}
      className="pes-chart"
      role="img"
      viewBox={`0 0 ${width} ${height}`}
    >
      <line className="pes-chart__axis" x1="32" x2={width - 10} y1={chartBottom} y2={chartBottom} />
      <text className="pes-chart__scale" x="5" y={chartTop + 4}>{formatCount(max)}</text>
      <text className="pes-chart__scale" x="17" y={chartBottom + 4}>0</text>
      {points.map((point, index) => {
        const x = 40 + ((index + 0.5) * usableWidth) / Math.max(points.length, 1);
        const barHeight = point.count > 0 ? Math.max(2, (point.count / max) * chartHeight) : 1;
        const labelVisible = index % step === 0 || index === points.length - 1;
        return (
          <g key={`${point.label}-${index}`}>
            <rect
              className="pes-chart__bar"
              height={barHeight}
              rx="2"
              width={barWidth}
              x={x - barWidth / 2}
              y={chartBottom - barHeight}
            >
              <title>{`${point.label}: ${formatCount(point.count)}`}</title>
            </rect>
            {labelVisible && (
              <text
                className="pes-chart__label"
                textAnchor="middle"
                transform={`translate(${x} ${chartBottom + 17}) rotate(${daily ? 0 : -35})`}
              >
                {point.label}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}

function TrendSection({ result }: { result: PlayerEntryStatisticsResponse }) {
  return (
    <section className="pes-card pes-card--chart" aria-labelledby="pes-trend-heading">
      <div className="pes-section-heading">
        <div>
          <h2 id="pes-trend-heading">时间趋势</h2>
          <p>按上海时间展示 {result.granularity} 粒度，空档时段也会保留为 0。</p>
        </div>
        <BarChart3 aria-hidden="true" className="pes-section-heading__icon" size={18} />
      </div>
      <TrendChart ariaLabel="进入趋势图" points={result.trend} />
      <div className="pes-data-table-wrap">
        <table aria-label="进入趋势数据" className="pes-data-table">
          <thead><tr><th>时间桶</th><th>响应次数</th></tr></thead>
          <tbody>
            {result.trend.map((point) => (
              <tr key={point.bucketStartUtc}><td>{point.label}</td><td>{formatCount(point.count)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function DailyPatternSection({ result }: { result: PlayerEntryStatisticsResponse }) {
  const points = result.dailyPattern.length > 0
    ? result.dailyPattern
    : Array.from({ length: 24 }, (_, hour) => ({ hour, label: `${String(hour).padStart(2, '0')}:00`, count: 0 }));
  return (
    <section className="pes-card pes-card--chart" aria-labelledby="pes-daily-heading">
      <div className="pes-section-heading">
        <div>
          <h2 id="pes-daily-heading">上海时间整日规律</h2>
          <p>按上海本地 0–23 点累计，帮助识别一天内的进入时段规律。</p>
        </div>
        <span className="pes-timezone-tag">{TIME_ZONE}</span>
      </div>
      <TrendChart ariaLabel="上海时间 0 到 23 点进入规律图" daily points={points} />
      <div className="pes-data-table-wrap">
        <table aria-label="上海时间整日规律数据" className="pes-data-table">
          <thead><tr><th>上海时间</th><th>响应次数</th></tr></thead>
          <tbody>
            {points.map((point) => (
              <tr key={point.hour}><td>{point.label}</td><td>{formatCount(point.count)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

export function PlayerEntryStatisticsView({ agents, targets }: PlayerEntryStatisticsViewProps) {
  const initialRange = useRef(createInitialRange());
  const [fromInput, setFromInput] = useState(initialRange.current.from);
  const [toInput, setToInput] = useState(initialRange.current.to);
  const [granularity, setGranularity] = useState<PlayerEntryStatisticsGranularity>('auto');
  const [lobbyType, setLobbyType] = useState<PlayerEntryStatisticsLobbyType>('all');
  const [targetMode, setTargetMode] = useState<PlayerEntryStatisticsTargetMode>('all');
  const [agentId, setAgentId] = useState('');
  const [targetServerId, setTargetServerId] = useState('');
  const [result, setResult] = useState<PlayerEntryStatisticsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const requestSequence = useRef(0);

  const loadStatistics = useCallback(async (nextQuery: PlayerEntryStatisticsQuery) => {
    const requestId = ++requestSequence.current;
    setIsLoading(true);
    setError(null);
    try {
      const response = await ApiService.fetchPlayerEntryStatistics(nextQuery);
      if (requestId !== requestSequence.current) return;
      if (!response.data) {
        setError(response.error || '读取玩家进入统计失败');
        return;
      }
      setResult(response.data);
    } catch {
      if (requestId !== requestSequence.current) return;
      setError('读取玩家进入统计失败');
    } finally {
      if (requestId === requestSequence.current) setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    const initialQuery = queryFromInputs(
      initialRange.current.from,
      initialRange.current.to,
      'auto',
      'all',
      'all',
      '',
      '',
    );
    if (initialQuery) void loadStatistics(initialQuery);
  }, [loadStatistics]);

  const submit = () => {
    const nextQuery = queryFromInputs(fromInput, toInput, granularity, lobbyType, targetMode, agentId, targetServerId);
    if (!nextQuery) {
      setFormError('请输入有效的上海时间范围。');
      return;
    }
    if (Date.parse(nextQuery.from!) >= Date.parse(nextQuery.to!)) {
      setFormError('结束时间必须晚于开始时间。');
      return;
    }
    setFormError(null);
    void loadStatistics(nextQuery);
  };

  const applyQuickRange = (hours: RangePreset) => {
    const to = new Date();
    const from = new Date(to.getTime() - (hours * 60 * 60 * 1000));
    const nextFrom = formatShanghaiDateTimeInput(from);
    const nextTo = formatShanghaiDateTimeInput(to);
    setFromInput(nextFrom);
    setToInput(nextTo);
    setFormError(null);
    const nextQuery = queryFromInputs(nextFrom, nextTo, granularity, lobbyType, targetMode, agentId, targetServerId);
    if (nextQuery) void loadStatistics(nextQuery);
  };

  const retry = () => submit();

  return (
    <div className="pes-page">
      <div className="pes-page-heading">
        <div>
          <div className="pes-eyebrow">运营分析 / ReplyJoinData</div>
          <h1>玩家进入统计</h1>
          <p>以成功发送 ReplyJoinData 的响应次数衡量暖服入口，不代表玩家已经连接游戏服务器。</p>
        </div>
        <div className="pes-page-heading__mark" aria-hidden="true"><BarChart3 size={22} /></div>
      </div>

      <section className="pes-card pes-filters" aria-label="统计筛选条件">
        <div className="pes-quick-range">
          <span className="pes-control-caption">快捷范围</span>
          <button type="button" onClick={() => applyQuickRange(24)}>最近 24 小时</button>
          <button type="button" onClick={() => applyQuickRange(168)}>最近 7 天</button>
          <button type="button" onClick={() => applyQuickRange(720)}>最近 30 天</button>
        </div>
        <div className="pes-filter-grid">
          <label>开始时间（上海）<input aria-label="开始时间（上海）" type="datetime-local" value={fromInput} onChange={(event) => setFromInput(event.target.value)} /></label>
          <label>结束时间（上海）<input aria-label="结束时间（上海）" type="datetime-local" value={toInput} onChange={(event) => setToInput(event.target.value)} /></label>
          <label>趋势粒度<select aria-label="趋势粒度" value={granularity} onChange={(event) => setGranularity(event.target.value as PlayerEntryStatisticsGranularity)}><option value="auto">自动</option><option value="hour">小时</option><option value="day">日</option><option value="week">周</option></select></label>
          <label>大厅类型<select aria-label="大厅类型" value={lobbyType} onChange={(event) => setLobbyType(event.target.value as PlayerEntryStatisticsLobbyType)}><option value="all">全部大厅</option><option value="standard">标准大厅</option><option value="reserved">预留大厅</option></select></label>
          <label>目标模式<select aria-label="目标模式" value={targetMode} onChange={(event) => setTargetMode(event.target.value as PlayerEntryStatisticsTargetMode)}><option value="all">全部模式</option><option value="coop">合作 coop</option><option value="versus">对抗 versus</option></select><small>未指定模式会归入 versus。</small></label>
          <label>Agent 筛选<select aria-label="Agent 筛选" value={agentId} onChange={(event) => setAgentId(event.target.value)}><option value="">全部 Agent</option>{agents.map((agent) => <option key={agent.id} value={agent.id}>{agent.name}</option>)}</select></label>
          <label>Target Server 筛选<select aria-label="Target Server 筛选" value={targetServerId} onChange={(event) => setTargetServerId(event.target.value)}><option value="">全部 Target Server</option>{targets.map((target) => <option key={target.id} value={target.id}>{target.name || target.endpoint}</option>)}</select></label>
          <div className="pes-filter-action"><button className="pes-query-button" type="button" onClick={submit} disabled={isLoading}><Search size={15} />查询统计</button></div>
        </div>
        {formError && <p className="pes-form-error" role="alert">{formError}</p>}
      </section>

      {isLoading && (
        <div className="pes-state pes-state--loading" role="status"><Loader2 className="pes-spin" size={22} /><span>正在读取玩家进入统计</span></div>
      )}

      {error && !isLoading && (
        <div className="pes-state pes-state--error" role="alert"><AlertCircle size={20} /><div><strong>读取玩家进入统计失败</strong><p>{error}</p><button type="button" onClick={retry}><RefreshCw size={14} />重试</button></div></div>
      )}

      {result && !isLoading && !error && (
        <>
          <div className="pes-summary-grid">
            <div className="pes-summary-card"><span>响应次数</span><strong>{formatCount(result.totalEntries)}</strong><small>成功 ReplyJoinData 响应</small></div>
            <div className="pes-summary-card pes-summary-card--accent"><span>平均每小时进入</span><strong>{formatRate(result.entriesPerHour)}</strong><small>按实际查询小时数计算</small></div>
            <div className="pes-summary-card"><span>查询范围</span><strong className="pes-summary-card__range">{formatShanghaiDateTime(result.fromUtc)}<br />至 {formatShanghaiDateTime(result.toUtc)}</strong><small>{result.timeZone || TIME_ZONE} · {result.granularity}</small></div>
          </div>
          <div className="pes-note"><span>统计口径</span>成功 ReplyJoinData 响应，不代表真实进服；未配置 Agent 下载区域显示为“默认”。</div>

          {result.totalEntries === 0 && (
            <div className="pes-state pes-state--empty" role="status"><BarChart3 size={24} /><strong>选定范围内暂无进入事件</strong><p>可以扩大时间范围，或调整 Agent、Target Server、大厅类型和目标模式筛选。</p></div>
          )}
          <>
            <div className="pes-chart-grid"><TrendSection result={result} /><DailyPatternSection result={result} /></div>
            <div className="pes-table-grid">
              <section className="pes-card" aria-labelledby="pes-agent-heading">
                <div className="pes-section-heading"><div><h2 id="pes-agent-heading">Agent 统计</h2><p>按 Agent 统计进入响应及频率；名称快照用于识别历史改名。</p></div></div>
                <div className="pes-data-table-wrap"><table aria-label="Agent 统计" className="pes-data-table"><thead><tr><th>Agent</th><th>响应次数</th><th>每小时</th><th>最近响应（上海）</th></tr></thead><tbody>{result.agents.length === 0 ? <tr><td colSpan={4}>暂无 Agent 数据</td></tr> : result.agents.map((agent) => <tr key={agent.agentId}><td><strong>{agent.agentName}</strong>{agent.nameSnapshots.length > 1 && <small className="pes-table-note" title={agent.nameSnapshots.join('、')}>历史名称：{agent.nameSnapshots.join('、')}</small>}</td><td>{formatCount(agent.entries)}</td><td>{formatRate(agent.entriesPerHour)}</td><td>{formatShanghaiDateTime(agent.lastEntryAtUtc)}</td></tr>)}</tbody></table></div>
              </section>
              <section className="pes-card" aria-labelledby="pes-region-heading">
                <div className="pes-section-heading"><div><h2 id="pes-region-heading">下载区域统计</h2><p>只使用 Agent 的显式配置，未配置的历史事件归入默认。</p></div></div>
                <div className="pes-data-table-wrap"><table aria-label="下载区域统计" className="pes-data-table"><thead><tr><th>下载区域</th><th>响应次数</th><th>每小时</th></tr></thead><tbody>{result.downloadRegions.length === 0 ? <tr><td colSpan={3}>暂无下载区域数据</td></tr> : result.downloadRegions.map((region, index) => <tr key={region.key ?? `default-${index}`}><td><strong>{region.key == null ? '默认' : region.label}</strong></td><td>{formatCount(region.entries)}</td><td>{formatRate(region.entriesPerHour)}</td></tr>)}</tbody></table></div>
              </section>
            </div>
          </>
        </>
      )}
    </div>
  );
}
