import React, { useState, useEffect, useCallback, useRef } from 'react';
import type {
  PlayerEntryStatisticsGranularity,
  PlayerEntryStatisticsLobbyType,
  PlayerEntryStatisticsQuery,
  PlayerEntryStatisticsResponse,
  PlayerEntryStatisticsTargetMode,
  SteamDownloadRegion,
  TargetServer,
  WarmupAgent,
} from '../types';
import { ApiService, formatShanghaiDateTimeInput, shanghaiLocalDateTimeToUtcIso } from '../services/api';
import { formatStatisticsDownloadRegion, formatSteamDownloadRegion } from '../services/steam-download-region-display';
import {
  BarChart2,
  Calendar,
  Filter,
  RotateCw,
  Search,
  Loader2,
  AlertCircle,
  Clock,
  Info,
  Layers,
  ChevronDown,
  ChevronUp,
  Cpu,
  Globe,
  TrendingUp,
} from 'lucide-react';

interface PlayerEntryStatisticsViewProps {
  targets: readonly TargetServer[];
  agents: readonly WarmupAgent[];
  steamRegions?: readonly SteamDownloadRegion[];
}

type QuickRange = '24h' | '7d' | '30d' | 'custom';

function formatShanghaiDateTime(value: string | null | undefined): string {
  if (!value) return '--';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '--';
  return formatShanghaiDateTimeInput(date).replace('T', ' ');
}

function formatCount(value: number): string {
  return new Intl.NumberFormat('zh-CN').format(value);
}

function formatRate(value: number): string {
  if (!Number.isFinite(value)) return '0';
  return value.toFixed(2).replace(/\.00$/, '').replace(/(\.\d)0$/, '$1');
}

function granularityLabel(value: PlayerEntryStatisticsResponse['granularity']): string {
  return value === 'hour' ? '按小时' : value === 'day' ? '按日' : '按周';
}

function toShanghaiDateTimeLocal(date: Date): string {
  return formatShanghaiDateTimeInput(date);
}

export const PlayerEntryStatisticsView: React.FC<PlayerEntryStatisticsViewProps> = ({
  targets,
  agents,
  steamRegions = [],
}) => {
  // Filter States
  const [quickRange, setQuickRange] = useState<QuickRange>('24h');
  const [startTimeLocal, setStartTimeLocal] = useState<string>(() => {
    const d = new Date(Date.now() - 24 * 3600 * 1000);
    return formatShanghaiDateTimeInput(d);
  });
  const [endTimeLocal, setEndTimeLocal] = useState<string>(() => {
    return formatShanghaiDateTimeInput(new Date());
  });

  const [granularity, setGranularity] = useState<PlayerEntryStatisticsGranularity>('auto');
  const [lobbyType, setLobbyType] = useState<PlayerEntryStatisticsLobbyType>('all');
  const [targetMode, setTargetMode] = useState<PlayerEntryStatisticsTargetMode>('all');
  const [selectedAgentId, setSelectedAgentId] = useState<string>('');
  const [selectedTargetId, setSelectedTargetId] = useState<string>('');

  // Request & Data State
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isQuerying, setIsQuerying] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [data, setData] = useState<PlayerEntryStatisticsResponse | null>(null);

  // Toggle table views for charts
  const [showTimeSeriesTable, setShowTimeSeriesTable] = useState<boolean>(false);
  const [showDailyTable, setShowDailyTable] = useState<boolean>(false);

  // Mouse-following Floating Tooltip State
  const [floatingTooltip, setFloatingTooltip] = useState<{
    visible: boolean;
    x: number;
    y: number;
    title: string;
    count: number;
    percentage: string;
    category: string;
    detail?: string;
    theme: 'blue' | 'emerald';
  }>({
    visible: false,
    x: 0,
    y: 0,
    title: '',
    count: 0,
    percentage: '0.0%',
    category: '',
    detail: '',
    theme: 'blue',
  });

  // Request ID ref to prevent race condition (later responses won't overwrite newer queries)
  const requestIdRef = useRef<number>(0);

  // Mouse event handlers for charts
  const handleChartMouseMove = (
    e: React.MouseEvent,
    title: string,
    count: number,
    category: string,
    detail: string | undefined,
    theme: 'blue' | 'emerald'
  ) => {
    const total = data?.totalEntries || 0;
    const percentage = total > 0 ? `${((count / total) * 100).toFixed(1)}%` : '0.0%';
    setFloatingTooltip({
      visible: true,
      x: e.clientX,
      y: e.clientY,
      title,
      count,
      percentage,
      category,
      detail,
      theme,
    });
  };

  const handleChartMouseLeave = () => {
    setFloatingTooltip((prev) => ({ ...prev, visible: false }));
  };

  // Quick range button handler
  const handleSelectQuickRange = (range: QuickRange) => {
    setQuickRange(range);
    const now = new Date();
    let start: Date;
    if (range === '24h') {
      start = new Date(now.getTime() - 24 * 3600 * 1000);
    } else if (range === '7d') {
      start = new Date(now.getTime() - 7 * 24 * 3600 * 1000);
    } else if (range === '30d') {
      start = new Date(now.getTime() - 30 * 24 * 3600 * 1000);
    } else {
      return;
    }

    const startStr = toShanghaiDateTimeLocal(start);
    const endStr = toShanghaiDateTimeLocal(now);
    setStartTimeLocal(startStr);
    setEndTimeLocal(endStr);
    setValidationError(null);

    // Auto-trigger search on quick range click
    executeSearch({
      from: shanghaiLocalDateTimeToUtcIso(startStr) ?? undefined,
      to: shanghaiLocalDateTimeToUtcIso(endStr) ?? undefined,
      granularity,
      lobbyType,
      targetMode,
      agentId: selectedAgentId || undefined,
      targetServerId: selectedTargetId || undefined,
    });
  };

  // Search execution
  const executeSearch = useCallback(
    async (params?: PlayerEntryStatisticsQuery) => {
      const currentRequestId = ++requestIdRef.current;
      setIsQuerying(true);
      setErrorMessage(null);
      setValidationError(null);

      const startUtc = params?.from || shanghaiLocalDateTimeToUtcIso(startTimeLocal);
      const endUtc = params?.to || shanghaiLocalDateTimeToUtcIso(endTimeLocal);

      if (!startUtc || !endUtc) {
        setValidationError('请输入有效的上海时间范围。');
        setIsQuerying(false);
        setIsLoading(false);
        return;
      }

      if (new Date(endUtc).getTime() <= new Date(startUtc).getTime()) {
        setValidationError('结束时间必须晚于开始时间，请重新调整时间范围。');
        setIsQuerying(false);
        setIsLoading(false);
        return;
      }

      const queryParams: PlayerEntryStatisticsQuery = {
        from: startUtc,
        to: endUtc,
        granularity: params?.granularity ?? granularity,
        lobbyType: params?.lobbyType ?? lobbyType,
        targetMode: params?.targetMode ?? targetMode,
        agentId: params?.agentId ?? (selectedAgentId || undefined),
        targetServerId: params?.targetServerId ?? (selectedTargetId || undefined),
      };

      try {
        const response = await ApiService.fetchPlayerEntryStatistics(queryParams);
        // Only update state if this is still the latest request
        if (currentRequestId === requestIdRef.current) {
          if (response.data) {
            setData(response.data);
          } else {
            setErrorMessage(`读取玩家进入统计失败：${response.error || '服务未返回统计数据'}`);
          }
        }
      } catch (err: any) {
        if (currentRequestId === requestIdRef.current) {
          setErrorMessage(`读取玩家进入统计失败：${err?.message || '网络连接异常，无法读取统计数据'}`);
        }
      } finally {
        if (currentRequestId === requestIdRef.current) {
          setIsQuerying(false);
          setIsLoading(false);
        }
      }
    },
    [startTimeLocal, endTimeLocal, granularity, lobbyType, targetMode, selectedAgentId, selectedTargetId]
  );

  // Initial load: 24h default
  useEffect(() => {
    executeSearch();
  }, []);

  const handleManualSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setQuickRange('custom');
    executeSearch();
  };

  // Max value calculations for charts
  const maxTimeSeriesCount = data?.trend.reduce((max, item) => Math.max(max, item.count), 0) || 1;
  const maxDailyCount = data?.dailyPattern.reduce((max, item) => Math.max(max, item.count), 0) || 1;

  const totalEventCount = data?.totalEntries || 0;

  return (
    <div className="space-y-5 pb-12">
      {/* 1. Page Title & Disclaimer Area */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="space-y-1">
          <div className="flex items-center gap-2.5">
            <h1 className="text-xl font-bold text-slate-900">玩家进入统计</h1>
            <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-mono font-medium bg-blue-50 text-blue-700 border border-blue-200">
              上海时间 Asia/Shanghai · UTC+8
            </span>
          </div>
          <p className="text-xs text-slate-600 leading-relaxed max-w-3xl">
            以暖服节点成功发送 <span className="font-semibold text-slate-800">ReplyJoinData</span>{' '}
            的响应次数衡量暖服大厅入口事件与匹配引导活跃度，
            <span className="text-slate-900 font-medium">不代表玩家已连接或成功进入目标游戏服务器</span>。
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <button
            type="button"
            onClick={() => executeSearch()}
            disabled={isQuerying}
            className="inline-flex items-center gap-1.5 px-3 py-2 text-xs text-slate-700 hover:text-slate-900 bg-white hover:bg-slate-50 border border-slate-200 rounded-md shadow-2xs transition-colors disabled:opacity-50"
          >
            <RotateCw className={`w-3.5 h-3.5 ${isQuerying ? 'animate-spin text-blue-600' : ''}`} />
            <span>刷新统计</span>
          </button>
        </div>
      </div>

      {isLoading && !data && (
        <div className="flex items-center gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-xs text-blue-800" role="status">
          <Loader2 className="h-4 w-4 animate-spin text-blue-600" />
          <div>
            <div className="font-medium">正在读取玩家进入统计</div>
            <div className="mt-0.5 text-[11px] text-blue-600">正在按上海时区聚合 ReplyJoinData 响应记录</div>
          </div>
        </div>
      )}

      {/* 2. Filter & Dimension Card */}
      <form onSubmit={handleManualSearch} className="bg-white p-5 rounded-lg border border-slate-200 shadow-xs space-y-4">
        <div className="flex items-center gap-2 pb-2.5 border-b border-slate-100">
          <Filter className="w-4 h-4 text-blue-600" />
          <h2 className="text-sm font-semibold text-slate-900">统计条件筛选</h2>
        </div>

        {/* Validation error */}
        {validationError && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-md text-xs text-red-800 flex items-start gap-2">
            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
            <span>{validationError}</span>
          </div>
        )}

        {/* Section 1: Time Range */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <label className="text-xs font-semibold text-slate-700 flex items-center gap-1.5">
              <Calendar className="w-3.5 h-3.5 text-slate-500" />
              <span>时间范围（上海时间 UTC+8）</span>
            </label>
            <div className="inline-flex items-center bg-slate-100 p-0.5 rounded-md text-xs">
              <button
                type="button"
                onClick={() => handleSelectQuickRange('24h')}
                className={`px-2.5 py-0.5 rounded transition-all font-medium ${
                  quickRange === '24h' ? 'bg-white text-blue-700 shadow-2xs font-semibold' : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                最近 24 小时
              </button>
              <button
                type="button"
                onClick={() => handleSelectQuickRange('7d')}
                className={`px-2.5 py-0.5 rounded transition-all font-medium ${
                  quickRange === '7d' ? 'bg-white text-blue-700 shadow-2xs font-semibold' : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                最近 7 天
              </button>
              <button
                type="button"
                onClick={() => handleSelectQuickRange('30d')}
                className={`px-2.5 py-0.5 rounded transition-all font-medium ${
                  quickRange === '30d' ? 'bg-white text-blue-700 shadow-2xs font-semibold' : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                最近 30 天
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <label htmlFor="input-start-time" className="block text-[11px] text-slate-500 mb-1">
                开始时间（上海）
              </label>
              <input
                id="input-start-time"
                type="datetime-local"
                value={startTimeLocal}
                onChange={(e) => {
                  setStartTimeLocal(e.target.value);
                  setQuickRange('custom');
                }}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
                required
              />
            </div>

            <div>
              <label htmlFor="input-end-time" className="block text-[11px] text-slate-500 mb-1">
                结束时间（上海）
              </label>
              <input
                id="input-end-time"
                type="datetime-local"
                value={endTimeLocal}
                onChange={(e) => {
                  setEndTimeLocal(e.target.value);
                  setQuickRange('custom');
                }}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
                required
              />
            </div>

            <div>
              <label htmlFor="select-granularity" className="block text-[11px] text-slate-500 mb-1">
                趋势粒度
              </label>
              <select
                id="select-granularity"
                value={granularity}
                onChange={(e) => setGranularity(e.target.value as any)}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
              >
                <option value="auto">自动判定 (依据时间跨度)</option>
                <option value="hour">按小时 (适合短周期)</option>
                <option value="day">按日 (适合周/月度)</option>
                <option value="week">按周 (适合季度趋势)</option>
              </select>
            </div>
          </div>
        </div>

        {/* Section 2: Analysis Dimensions */}
        <div className="pt-2 border-t border-slate-100">
          <div className="text-xs font-semibold text-slate-700 mb-2 flex items-center gap-1.5">
            <Layers className="w-3.5 h-3.5 text-slate-500" />
            <span>分析维度与过滤条件</span>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            {/* Lobby Type */}
            <div>
              <label htmlFor="select-lobby-type" className="block text-[11px] text-slate-500 mb-1">
                大厅类型
              </label>
              <select
                id="select-lobby-type"
                value={lobbyType}
                onChange={(e) => setLobbyType(e.target.value as any)}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
              >
                <option value="all">全部大厅类型</option>
                <option value="standard">标准公共匹配大厅</option>
                <option value="reserved">预留锁定大厅</option>
              </select>
            </div>

            {/* Game Mode */}
            <div>
              <label htmlFor="select-game-mode" className="block text-[11px] text-slate-500 mb-1">
                目标模式
              </label>
              <select
                id="select-game-mode"
                value={targetMode}
                onChange={(e) => setTargetMode(e.target.value as PlayerEntryStatisticsTargetMode)}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
              >
                <option value="all">全部目标模式</option>
                <option value="coop">合作模式</option>
                <option value="versus">对抗模式</option>
              </select>
              <p className="mt-1 text-[10px] text-slate-400">未指定模式按默认逻辑处理。</p>
            </div>

            {/* Warmup Agent */}
            <div>
              <label htmlFor="select-agent-id" className="block text-[11px] text-slate-500 mb-1">
                暖服节点筛选
              </label>
              <select
                id="select-agent-id"
                value={selectedAgentId}
                onChange={(e) => setSelectedAgentId(e.target.value)}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
              >
                <option value="">全部暖服节点 ({agents.length})</option>
                {agents.map((ag) => (
                  <option key={ag.id} value={ag.id}>
                    {ag.name}（{ag.steamRegion ? formatSteamDownloadRegion(ag.steamRegion, steamRegions) : '默认区域'}）
                  </option>
                ))}
              </select>
            </div>

            {/* Target Server */}
            <div>
              <label htmlFor="select-target-id" className="block text-[11px] text-slate-500 mb-1">
                目标服务器筛选
              </label>
              <select
                id="select-target-id"
                value={selectedTargetId}
                onChange={(e) => setSelectedTargetId(e.target.value)}
                className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
              >
                <option value="">全部目标服务器 ({targets.length})</option>
                {targets.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name || t.endpoint}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* Action Button */}
        <div className="flex items-center justify-end pt-2">
          <button
            type="submit"
            disabled={isQuerying}
            className="inline-flex items-center justify-center gap-2 px-6 py-2 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-md shadow-xs focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:opacity-50 transition-colors"
          >
            {isQuerying ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>正在查询统计...</span>
              </>
            ) : (
              <>
                <Search className="w-4 h-4" />
                <span>查询统计</span>
              </>
            )}
          </button>
        </div>
      </form>

      {/* Error state */}
      {errorMessage && (
        <div role="alert" className="p-4 bg-red-50 border border-red-200 rounded-lg text-xs text-red-800 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-red-600 shrink-0" />
            <span>{errorMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => executeSearch()}
            className="px-3 py-1 text-xs font-medium text-red-700 hover:text-red-900 bg-white border border-red-300 rounded shadow-2xs hover:bg-red-50"
          >
            重试
          </button>
        </div>
      )}

      {/* 3. Summary Cards Area */}
      {data && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3.5">
            {/* Metric 1: Total Responses */}
            <div className="bg-white p-4 rounded-lg border border-blue-200 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between text-slate-600 mb-2">
                <span className="text-xs font-medium">响应总次数 (ReplyJoinData)</span>
                <TrendingUp className="w-4 h-4 text-blue-600" />
              </div>
              <div className="text-2xl font-bold text-blue-900 tracking-tight font-mono">
                {formatCount(data.totalEntries)} <span className="text-xs font-normal text-slate-500 font-sans">次</span>
              </div>
              <div className="text-[11px] text-slate-400 mt-1">
                成功发送大厅握手数据包的总量
              </div>
            </div>

            {/* Metric 2: Avg Per Hour */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between text-slate-600 mb-2">
                <span className="text-xs font-medium">平均每小时进入</span>
                <Clock className="w-4 h-4 text-emerald-600" />
              </div>
              <div className="text-2xl font-bold text-slate-900 tracking-tight font-mono">
                {formatRate(data.entriesPerHour)} <span className="text-xs font-normal text-slate-500 font-sans">次/小时</span>
              </div>
              <div className="text-[11px] text-slate-400 mt-1">
                统计时段内平摊频率（包含零事件时段）
              </div>
            </div>

            {/* Metric 3: Context & Granularity */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between text-slate-600 mb-2">
                <span className="text-xs font-medium">查询范围与统计粒度</span>
                <Info className="w-4 h-4 text-slate-400" />
              </div>
              <div className="text-xs font-semibold text-slate-800 space-y-0.5">
                <div>
                  时段：<span className="font-mono text-[11px] text-slate-600">{formatShanghaiDateTime(data.fromUtc)} ~ {formatShanghaiDateTime(data.toUtc)}</span>
                </div>
                <div className="text-slate-500 text-[11px]">
                  粒度：<span className="text-blue-700 font-medium">{granularityLabel(data.granularity)}</span> · 时区：<span className="font-mono">{data.timeZone}</span>
                </div>
              </div>
              <div className="text-[11px] text-slate-400 mt-1">
                所有时间均以中国标准时间 (UTC+8) 汇总结算
              </div>
            </div>
          </div>

          {/* Statistical Notice Banner */}
          <div className="p-3.5 bg-blue-50/70 border border-blue-200 rounded-lg text-xs text-blue-900 flex items-start gap-2.5">
            <Info className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
            <div className="leading-relaxed text-[11px]">
              <span className="font-semibold">统计口径重要说明：</span>
              此统计记录暖服节点成功向客户端发送 ReplyJoinData 数据包的次数，用于衡量大厅连接握手流量与匹配注入频次；
              <span className="font-medium underline">成功进入响应，不代表真实进服，也不等于 Steam 在线玩家数或大厅成员数</span>。
              未指定或历史未配置 Steam 下载区域的记录均统一归入“默认”展示。
            </div>
          </div>

          {/* Empty State Warning */}
          {totalEventCount === 0 && (
            <div className="p-8 bg-slate-50 border border-slate-200 rounded-lg text-center space-y-2">
              <BarChart2 className="w-8 h-8 text-slate-300 mx-auto" />
              <div className="text-sm font-semibold text-slate-700">选定范围内暂无进入事件</div>
              <div className="text-xs text-slate-500 max-w-md mx-auto">
                在当前选定的时间段和筛选条件下未记录到 ReplyJoinData 成功响应，可尝试扩大时间范围或放宽筛选条件。
              </div>
            </div>
          )}

          {/* 4. Trends Area (Two Charts in responsive grid) */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {/* Chart 1: Time Trend */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs space-y-3">
              <div className="flex items-center justify-between pb-2 border-b border-slate-100">
                <div className="flex items-center gap-2">
                  <BarChart2 className="w-4 h-4 text-blue-600" />
                  <h3 className="text-xs font-semibold text-slate-900">时间趋势（何时发生）</h3>
                </div>
                <button
                  type="button"
                  onClick={() => setShowTimeSeriesTable(!showTimeSeriesTable)}
                  className="text-[11px] text-blue-600 hover:underline flex items-center gap-1"
                >
                  <span>{showTimeSeriesTable ? '收起数据表' : '查看数据表'}</span>
                  {showTimeSeriesTable ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
                </button>
              </div>

              {/* Lightweight SVG Bar Chart */}
              <div className="h-44 w-full flex items-end gap-1 pt-4 pb-1 overflow-x-auto" role="img" aria-label="进入趋势图">
                {data.trend.length === 0 ? (
                  <div className="w-full h-full flex items-center justify-center text-xs text-slate-400">
                    暂无趋势数据
                  </div>
                ) : (
                  data.trend.map((item, idx) => {
                    const heightPercent = maxTimeSeriesCount > 0 ? (item.count / maxTimeSeriesCount) * 100 : 0;
                    return (
                      <div
                        key={idx}
                        className="flex-1 min-w-[14px] flex flex-col items-center h-full justify-end group cursor-pointer py-1"
                        onMouseMove={(e) =>
                          handleChartMouseMove(
                            e,
                            `时间：${item.label}`,
                            item.count,
                            granularityLabel(data.granularity),
                            `ReplyJoinData 握手事件 · 上海时间 (UTC+8)`,
                            'blue'
                          )
                        }
                        onMouseEnter={(e) =>
                          handleChartMouseMove(
                            e,
                            `时间：${item.label}`,
                            item.count,
                            granularityLabel(data.granularity),
                            `ReplyJoinData 握手事件 · 上海时间 (UTC+8)`,
                            'blue'
                          )
                        }
                        onMouseLeave={handleChartMouseLeave}
                      >
                        {/* Bar */}
                        <div
                          className={`w-full rounded-t-xs transition-colors duration-150 ${
                            item.count > 0 ? 'bg-blue-600 group-hover:bg-blue-500' : 'bg-slate-100 group-hover:bg-slate-200'
                          }`}
                          style={{ height: `${Math.max(4, heightPercent)}%` }}
                        />
                      </div>
                    );
                  })
                )}
              </div>

              {/* Time axis start/end */}
              {data.trend.length > 0 && (
                <div className="flex items-center justify-between text-[10px] text-slate-400 font-mono pt-1 border-t border-slate-100">
                  <span>{data.trend[0].label}</span>
                  <span>{data.trend[Math.floor(data.trend.length / 2)]?.label}</span>
                  <span>{data.trend[data.trend.length - 1].label}</span>
                </div>
              )}

              {/* Data Table View */}
              {showTimeSeriesTable && (
                <div className="max-h-48 overflow-y-auto border border-slate-200 rounded mt-2">
                  <table className="w-full text-left text-xs border-collapse font-mono">
                    <thead className="bg-slate-50 text-slate-600 sticky top-0 border-b border-slate-200">
                      <tr>
                        <th className="py-1.5 px-3">时间桶 (上海)</th>
                        <th className="py-1.5 px-3 text-right">响应次数</th>
                        <th className="py-1.5 px-3 text-right">占比</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100 text-slate-700 text-[11px]">
                      {data.trend.map((item, idx) => (
                        <tr key={idx} className="hover:bg-slate-50">
                          <td className="py-1 px-3">{item.label}</td>
                          <td className="py-1 px-3 text-right font-bold text-slate-900">{formatCount(item.count)}</td>
                          <td className="py-1 px-3 text-right text-slate-500">
                            {totalEventCount > 0 ? `${((item.count / totalEventCount) * 100).toFixed(1)}%` : '0.0%'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            {/* Chart 2: 24h Shanghai Daily Distribution */}
            <div className="bg-white p-4 rounded-lg border border-slate-200 shadow-xs space-y-3">
              <div className="flex items-center justify-between pb-2 border-b border-slate-100">
                <div className="flex items-center gap-2">
                  <Clock className="w-4 h-4 text-emerald-600" />
                  <h3 className="text-xs font-semibold text-slate-900">上海时间整日规律</h3>
                  <span className="text-[10px] text-slate-400">（00:00 - 23:00）</span>
                </div>
                <button
                  type="button"
                  onClick={() => setShowDailyTable(!showDailyTable)}
                  className="text-[11px] text-blue-600 hover:underline flex items-center gap-1"
                >
                  <span>{showDailyTable ? '收起数据表' : '查看数据表'}</span>
                  {showDailyTable ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
                </button>
              </div>

              {/* 24-hour bar chart */}
              <div className="h-44 w-full flex items-end gap-1 pt-4 pb-1" role="img" aria-label="上海时间 0 到 23 点进入规律图">
                {data.dailyPattern.map((item) => {
                  const heightPercent = maxDailyCount > 0 ? (item.count / maxDailyCount) * 100 : 0;
                  const isPeakHour = item.hour >= 19 && item.hour <= 23;
                  const isLateNight = item.hour >= 1 && item.hour <= 6;
                  const detail = isPeakHour
                    ? '🔥 晚间核心高峰时段 (19:00 - 23:00)'
                    : isLateNight
                    ? '🌙 凌晨低频时段'
                    : '☀️ 常规活跃时段';

                  return (
                    <div
                      key={item.hour}
                      className="flex-1 min-w-[6px] flex flex-col items-center h-full justify-end group cursor-pointer py-1"
                      onMouseMove={(e) =>
                        handleChartMouseMove(
                          e,
                          `时段：${item.label}`,
                          item.count,
                          isPeakHour ? '晚高峰' : '整日规律',
                          detail,
                          'emerald'
                        )
                      }
                      onMouseEnter={(e) =>
                        handleChartMouseMove(
                          e,
                          `时段：${item.label}`,
                          item.count,
                          isPeakHour ? '晚高峰' : '整日规律',
                          detail,
                          'emerald'
                        )
                      }
                      onMouseLeave={handleChartMouseLeave}
                    >
                      {/* Bar */}
                      <div
                        className={`w-full rounded-t-xs transition-colors duration-150 ${
                          item.count === 0
                            ? 'bg-slate-100 group-hover:bg-slate-200'
                            : isPeakHour
                            ? 'bg-emerald-600 group-hover:bg-emerald-500'
                            : 'bg-emerald-400 group-hover:bg-emerald-300'
                        }`}
                        style={{ height: `${Math.max(4, heightPercent)}%` }}
                      />
                    </div>
                  );
                })}
              </div>

              {/* Hour scale */}
              <div className="flex items-center justify-between text-[10px] text-slate-400 font-mono pt-1 border-t border-slate-100">
                <span>00:00</span>
                <span>06:00</span>
                <span>12:00</span>
                <span className="text-emerald-700 font-bold">19:00 (高峰)</span>
                <span>23:00</span>
              </div>

              {/* Daily Table View */}
              {showDailyTable && (
                <div className="max-h-48 overflow-y-auto border border-slate-200 rounded mt-2">
                  <table className="w-full text-left text-xs border-collapse font-mono">
                    <thead className="bg-slate-50 text-slate-600 sticky top-0 border-b border-slate-200">
                      <tr>
                        <th className="py-1.5 px-3">时段 (上海时间)</th>
                        <th className="py-1.5 px-3 text-right">累计响应</th>
                        <th className="py-1.5 px-3 text-right">占比</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100 text-slate-700 text-[11px]">
                      {data.dailyPattern.map((item) => (
                        <tr key={item.hour} className="hover:bg-slate-50">
                          <td className="py-1 px-3">{item.label}</td>
                          <td className="py-1 px-3 text-right font-bold text-slate-900">{formatCount(item.count)}</td>
                          <td className="py-1 px-3 text-right text-slate-500">
                            {totalEventCount > 0 ? `${((item.count / totalEventCount) * 100).toFixed(1)}%` : '0.0%'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>

          {/* 5. Dimension Breakdown Tables (Agent Stats & Region Stats) */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {/* Agent Statistics */}
            <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
              <div className="px-4 py-3 bg-slate-50/70 border-b border-slate-200 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Cpu className="w-4 h-4 text-blue-600" />
                  <h3 className="text-xs font-semibold text-slate-900">暖服节点维度统计</h3>
                </div>
                <span className="text-[11px] text-slate-500 font-mono">
                  共 {data.agents.length} 个节点
                </span>
              </div>

              <div className="overflow-x-auto">
                <table aria-label="暖服节点统计" className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="bg-slate-100/70 text-slate-600 border-b border-slate-200 font-semibold">
                      <th className="py-2 px-3">暖服节点</th>
                      <th className="py-2 px-3 text-right font-mono">响应次数</th>
                      <th className="py-2 px-3 text-right font-mono">每小时</th>
                      <th className="py-2 px-3">最近响应时间 (上海)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 text-slate-700">
                    {data.agents.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="py-6 text-center text-slate-400 text-xs">
                          暂无节点数据
                        </td>
                      </tr>
                    ) : (
                      data.agents.map((ag) => (
                        <tr key={ag.agentId} className="hover:bg-slate-50/80">
                          <td className="py-2.5 px-3">
                            <div className="font-medium text-slate-900 font-mono">
                              {ag.agentName}
                            </div>
                            {ag.nameSnapshots.length > 1 && (
                              <div className="text-[10px] text-slate-400 font-mono">
                                历史名称：{ag.nameSnapshots.join('、')}
                              </div>
                            )}
                          </td>
                          <td className="py-2.5 px-3 text-right font-mono font-bold text-slate-900">
                            {formatCount(ag.entries)}
                          </td>
                          <td className="py-2.5 px-3 text-right font-mono text-slate-600">
                            {formatRate(ag.entriesPerHour)}
                          </td>
                          <td className="py-2.5 px-3 font-mono text-[11px] text-slate-500 whitespace-nowrap">
                            {formatShanghaiDateTime(ag.lastEntryAtUtc)}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Region Statistics */}
            <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
              <div className="px-4 py-3 bg-slate-50/70 border-b border-slate-200 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Globe className="w-4 h-4 text-emerald-600" />
                  <h3 className="text-xs font-semibold text-slate-900">Steam 下载区域统计</h3>
                </div>
                <span className="text-[11px] text-slate-500 font-mono">
                  未配置区域归入“默认”
                </span>
              </div>

              <div className="overflow-x-auto">
                <table aria-label="下载区域统计" className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="bg-slate-100/70 text-slate-600 border-b border-slate-200 font-semibold">
                      <th className="py-2 px-3">Steam 下载区域</th>
                      <th className="py-2 px-3 text-right font-mono">响应次数</th>
                      <th className="py-2 px-3 text-right font-mono">每小时</th>
                      <th className="py-2 px-3 text-right">占比</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 text-slate-700">
                    {data.downloadRegions.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="py-6 text-center text-slate-400 text-xs">
                          暂无下载区域数据
                        </td>
                      </tr>
                    ) : (
                      data.downloadRegions.map((reg, idx) => (
                        <tr key={idx} className="hover:bg-slate-50/80">
                          <td className="py-2.5 px-3 font-medium text-slate-900">
                            <span className="bg-slate-100 px-2 py-0.5 rounded text-xs border border-slate-200 font-mono">
                              {formatStatisticsDownloadRegion(reg.key, steamRegions)}
                            </span>
                          </td>
                          <td className="py-2.5 px-3 text-right font-mono font-bold text-slate-900">
                            {formatCount(reg.entries)}
                          </td>
                          <td className="py-2.5 px-3 text-right font-mono text-slate-600">
                            {formatRate(reg.entriesPerHour)}
                          </td>
                          <td className="py-2.5 px-3 text-right text-slate-600 text-xs font-mono">
                            {totalEventCount > 0 ? `${((reg.entries / totalEventCount) * 100).toFixed(1)}%` : '0.0%'}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Mouse-following Floating Tooltip - Fixed highest layer, never clipped or covered by any other component */}
      {floatingTooltip.visible && (
        <div
          className="fixed z-[9999] pointer-events-none transition-transform duration-75 ease-out select-none"
          style={{
            left: `${Math.min(
              Math.max(floatingTooltip.x, 110),
              typeof window !== 'undefined' ? window.innerWidth - 110 : floatingTooltip.x
            )}px`,
            top: floatingTooltip.y < 130 ? `${floatingTooltip.y + 16}px` : `${floatingTooltip.y - 12}px`,
            transform: floatingTooltip.y < 130 ? 'translate(-50%, 0)' : 'translate(-50%, -100%)',
          }}
        >
          <div className="px-3.5 py-2.5 bg-slate-900/95 text-white text-xs rounded-lg shadow-2xl border border-slate-700/80 backdrop-blur-xs font-mono flex flex-col gap-1.5 min-w-[190px] max-w-[320px]">
            {/* Header info */}
            <div className="flex items-center justify-between gap-3 border-b border-slate-700/80 pb-1.5">
              <span className="font-semibold text-slate-200 text-[11px] truncate tracking-tight">
                {floatingTooltip.title}
              </span>
              <span
                className={`text-[10px] px-1.5 py-0.5 rounded font-sans font-medium shrink-0 ${
                  floatingTooltip.theme === 'emerald'
                    ? 'bg-emerald-950 text-emerald-300 border border-emerald-700/60'
                    : 'bg-blue-950 text-blue-300 border border-blue-700/60'
                }`}
              >
                {floatingTooltip.category}
              </span>
            </div>

            {/* Metric count */}
            <div className="flex items-baseline justify-between gap-3 pt-0.5">
              <span className="text-slate-400 text-[11px] font-sans">响应次数</span>
              <span
                className={`text-base font-bold ${
                  floatingTooltip.theme === 'emerald' ? 'text-emerald-400' : 'text-blue-400'
                }`}
              >
                {formatCount(floatingTooltip.count)}{' '}
                <span className="text-xs font-normal text-slate-400 font-sans">次</span>
              </span>
            </div>

            {/* Ratio */}
            <div className="flex items-center justify-between gap-3 text-[11px] text-slate-400">
              <span className="font-sans">占当前筛选总量</span>
              <span className="text-slate-200 font-bold font-mono">{floatingTooltip.percentage}</span>
            </div>

            {/* Optional detail */}
            {floatingTooltip.detail && (
              <div className="text-[10px] text-slate-300 font-sans pt-1 border-t border-slate-800/80 leading-relaxed">
                {floatingTooltip.detail}
              </div>
            )}
          </div>

          {/* Pointer Arrow */}
          <div
            className={`absolute left-1/2 transform -translate-x-1/2 w-0 h-0 border-x-4 border-x-transparent ${
              floatingTooltip.y < 130
                ? 'bottom-full border-b-4 border-b-slate-900/95 -mb-0'
                : 'top-full border-t-4 border-t-slate-900/95 -mt-0'
            }`}
          />
        </div>
      )}
    </div>
  );
};
