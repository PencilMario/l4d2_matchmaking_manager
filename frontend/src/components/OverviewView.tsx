import React, { useEffect, useMemo, useState } from 'react';
import {
  TargetServer,
  WarmupAgent,
  WarmupAttempt,
  ServerObservation,
  TabKey,
} from '../types';
import {
  formatA2sStatus,
  formatAttemptPhase,
  formatAttemptStatus,
  formatDateTime,
  formatOperationMode,
  formatSecondsToTime,
} from '../utils/statusMapping';
import { Badge } from './common/Badge';
import { Tooltip } from './common/Tooltip';
import {
  Activity,
  AlertTriangle,
  ServerOff,
  Server,
  Cpu,
  ShieldAlert,
  ArrowRight,
  Clock,
  ExternalLink,
  ChevronDown,
  ChevronRight,
  ChevronsUpDown,
  CornerDownRight,
  Search,
  Users,
} from 'lucide-react';

interface OverviewViewProps {
  targets: TargetServer[];
  agents: WarmupAgent[];
  attempts: WarmupAttempt[];
  observations: ServerObservation[];
  onNavigateToTarget: (targetId: string) => void;
  onNavigateTab: (tab: TabKey) => void;
}

export const OverviewView: React.FC<OverviewViewProps> = ({
  targets,
  agents,
  attempts,
  onNavigateToTarget,
  onNavigateTab,
}) => {
  // Summary counts
  const activeAttempts = attempts.filter((a) => a.status === 'active');
  const uncertainAttempts = attempts.filter((a) => a.status === 'uncertain');
  const unavailableServers = targets.filter((t) => t.a2sStatus === 'unavailable');
  const onlineServers = targets.filter((t) => t.a2sStatus === 'online');
  const runningAgents = agents.filter((a) => a.status === 'running');
  const quarantinedAgents = agents.filter((a) => a.status === 'quarantined');

  type ServerGroup = {
    server: TargetServer;
    attempts: WarmupAttempt[];
    activeAttempts: WarmupAttempt[];
    uncertainAttempts: WarmupAttempt[];
    associatedAgents: WarmupAgent[];
    serverRemainingSeconds: number;
    isServerActiveWarmup: boolean;
    isServerUncertain: boolean;
  };

  const groupedServers = useMemo<ServerGroup[]>(() => {
    const attemptGroups = new Map<string, WarmupAttempt[]>();
    attempts.forEach((attempt) => {
      const group = attemptGroups.get(attempt.targetServerId) ?? [];
      group.push(attempt);
      attemptGroups.set(attempt.targetServerId, group);
    });

    const serversById = new Map(targets.map((server) => [server.id, server]));
    const allServerIds = new Set([...targets.map((server) => server.id), ...attemptGroups.keys()]);

    return [...allServerIds].map((serverId) => {
      const serverAttempts = [...(attemptGroups.get(serverId) ?? [])].sort(
        (a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt),
      );
      const fallbackAttempt = serverAttempts[0];
      const server = serversById.get(serverId) ?? {
        id: serverId,
        endpoint: fallbackAttempt?.targetServerEndpoint ?? serverId,
        name: fallbackAttempt?.targetServerName,
        currentPlayers: 0,
        maxPlayers: 0,
        a2sStatus: 'pending' as const,
        enabled: true,
        priority: 0,
        requiresReservation: false,
        maxConcurrentWarmups: 0,
        activeWarmupsCount: serverAttempts.filter((attempt) => attempt.status === 'active').length,
        attemptWindowSeconds: fallbackAttempt?.totalSeconds ?? 720,
        playerTarget: 0,
        createdAt: fallbackAttempt?.startedAt ?? new Date(0).toISOString(),
        updatedAt: fallbackAttempt?.updatedAt ?? new Date(0).toISOString(),
      };
      const activeAttempts = serverAttempts.filter((attempt) => attempt.status === 'active');
      const uncertainAttempts = serverAttempts.filter((attempt) => attempt.status === 'uncertain');
      const agentIds = new Set(serverAttempts.map((attempt) => attempt.agentId));
      const associatedAgents = agents.filter((agent) => agentIds.has(agent.id));

      return {
        server,
        attempts: serverAttempts,
        activeAttempts,
        uncertainAttempts,
        associatedAgents,
        serverRemainingSeconds: activeAttempts.length > 0
          ? Math.min(...activeAttempts.map((attempt) => attempt.remainingSeconds))
          : 0,
        isServerActiveWarmup: activeAttempts.length > 0,
        isServerUncertain: uncertainAttempts.length > 0,
      };
    }).sort((a, b) => {
      const aStartedAt = a.attempts[0]?.startedAt ?? a.server.updatedAt;
      const bStartedAt = b.attempts[0]?.startedAt ?? b.server.updatedAt;
      return Date.parse(bStartedAt) - Date.parse(aStartedAt);
    });
  }, [agents, attempts, targets]);

  type ServerFilterType = 'all' | 'active' | 'uncertain' | 'unavailable';
  const [filterType, setFilterType] = useState<ServerFilterType>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedServerIds, setExpandedServerIds] = useState<Set<string>>(
    () => new Set(),
  );

  useEffect(() => {
    setExpandedServerIds((current) => {
      const next = new Set([...current].filter((id) => groupedServers.some((group) => group.server.id === id)));
      groupedServers.forEach((group) => {
        if (group.isServerActiveWarmup || group.isServerUncertain) next.add(group.server.id);
      });
      if (next.size === 0 && groupedServers.length > 0) next.add(groupedServers[0].server.id);
      return next;
    });
  }, [groupedServers]);

  const filteredGroupedServers = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return groupedServers.filter((group) => {
      if (filterType === 'active' && !group.isServerActiveWarmup) return false;
      if (filterType === 'uncertain' && !group.isServerUncertain) return false;
      if (filterType === 'unavailable' && group.server.a2sStatus !== 'unavailable') return false;
      if (!query) return true;

      return [
        group.server.name,
        group.server.endpoint,
        ...group.associatedAgents.map((agent) => agent.name),
        ...group.attempts.map((attempt) => attempt.lobbyId),
      ].some((value) => value?.toLowerCase().includes(query));
    });
  }, [filterType, groupedServers, searchQuery]);

  const toggleServerExpand = (serverId: string) => {
    setExpandedServerIds((current) => {
      const next = new Set(current);
      if (next.has(serverId)) next.delete(serverId);
      else next.add(serverId);
      return next;
    });
  };

  const toggleAllServers = () => {
    setExpandedServerIds((current) => current.size === groupedServers.length
      ? new Set()
      : new Set(groupedServers.map((group) => group.server.id)));
  };

  const summaryCards = [
    {
      label: '正在进行的暖服任务',
      count: activeAttempts.length,
      icon: <Activity className="w-4 h-4 text-blue-600" />,
      bg: 'bg-blue-50/50',
      border: 'border-blue-200',
      textColor: 'text-blue-900',
      onClick: () => setFilterType('active'),
    },
    {
      label: '结果未确认的任务',
      count: uncertainAttempts.length,
      icon: <AlertTriangle className="w-4 h-4 text-amber-600" />,
      bg: uncertainAttempts.length > 0 ? 'bg-amber-50' : 'bg-white',
      border: uncertainAttempts.length > 0 ? 'border-amber-300' : 'border-slate-200',
      textColor: uncertainAttempts.length > 0 ? 'text-amber-900 font-bold' : 'text-slate-900',
      onClick: () => setFilterType('uncertain'),
    },
    {
      label: 'A2S 不可用服务器',
      count: unavailableServers.length,
      icon: <ServerOff className="w-4 h-4 text-red-600" />,
      bg: unavailableServers.length > 0 ? 'bg-red-50' : 'bg-white',
      border: unavailableServers.length > 0 ? 'border-red-300' : 'border-slate-200',
      textColor: unavailableServers.length > 0 ? 'text-red-900 font-bold' : 'text-slate-900',
      onClick: () => setFilterType('unavailable'),
    },
    {
      label: '在线目标服务器',
      count: onlineServers.length,
      icon: <Server className="w-4 h-4 text-emerald-600" />,
      bg: 'bg-white',
      border: 'border-slate-200',
      textColor: 'text-slate-900',
      onClick: () => onNavigateTab('targets'),
    },
    {
      label: '运行中的暖服节点',
      count: runningAgents.length,
      icon: <Cpu className="w-4 h-4 text-emerald-600" />,
      bg: 'bg-white',
      border: 'border-slate-200',
      textColor: 'text-slate-900',
      onClick: () => onNavigateTab('agents'),
    },
    {
      label: '已隔离节点',
      count: quarantinedAgents.length,
      icon: <ShieldAlert className="w-4 h-4 text-amber-600" />,
      bg: quarantinedAgents.length > 0 ? 'bg-amber-50' : 'bg-white',
      border: quarantinedAgents.length > 0 ? 'border-amber-200' : 'border-slate-200',
      textColor: quarantinedAgents.length > 0 ? 'text-amber-900 font-bold' : 'text-slate-900',
      onClick: () => onNavigateTab('agents'),
    },
  ];

  return (
    <div className="space-y-6 pb-12">
      {/* Page Title */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-900">系统概览</h1>
      </div>

      {/* Metric Summary Grid (6 cards) */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3.5">
        {summaryCards.map((card, idx) => (
          <div
            key={idx}
            onClick={card.onClick}
            className={`p-3.5 rounded-lg border ${card.border} ${card.bg} shadow-xs flex flex-col justify-between transition-all cursor-pointer hover:shadow-sm hover:border-slate-300`}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs text-slate-600 font-medium leading-tight line-clamp-1">
                {card.label}
              </span>
              {card.icon}
            </div>
            <div className={`text-2xl font-bold tracking-tight ${card.textColor}`}>
              {card.count}
            </div>
          </div>
        ))}
      </div>

      {/* Manual Intervention Area (人工处理区) */}
      {(uncertainAttempts.length > 0 || unavailableServers.length > 0) && (
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-semibold text-slate-900">人工处理区</h2>
            <span className="text-xs text-amber-800 bg-amber-100 px-2 py-0.5 rounded font-medium">
              需人工确认
            </span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Uncertain Attempts Card */}
            {uncertainAttempts.length > 0 && (
              <div className="bg-amber-50/70 border border-amber-300 rounded-lg p-4 space-y-3">
                <div className="flex items-start gap-2.5">
                  <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
                  <div className="flex-1 min-w-0">
                    <h3 className="text-xs font-semibold text-amber-900">
                      结果未确认暖服任务 ({uncertainAttempts.length} 项)
                    </h3>
                    <p className="text-xs text-amber-800 mt-0.5 leading-relaxed">
                      暖服时限已到但未能通过 A2S 确认玩家是否成功入服，请核查服务器日志或手动调度。
                    </p>
                  </div>
                </div>

                <div className="space-y-1.5 pt-1">
                  {uncertainAttempts.map((item) => (
                    <div
                      key={item.id}
                      className="bg-white/90 border border-amber-200 rounded p-2.5 flex items-center justify-between text-xs"
                    >
                      <div className="min-w-0 pr-2">
                        <div className="font-semibold text-slate-800 truncate">
                          {item.targetServerName || item.targetServerEndpoint}
                        </div>
                        <div className="text-[11px] text-slate-500 font-mono flex items-center gap-2 mt-0.5">
                          <span>节点: {item.agentName}</span>
                          <span>·</span>
                          <span>大厅: {item.lobbyId || '无'}</span>
                        </div>
                        {item.errorMessage && (
                          <div className="text-[11px] text-red-700 mt-1">
                            {item.errorMessage}
                          </div>
                        )}
                      </div>
                      <button
                        type="button"
                        onClick={() => onNavigateToTarget(item.targetServerId)}
                        className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-amber-900 bg-amber-100 hover:bg-amber-200 rounded border border-amber-300 transition-colors shrink-0"
                      >
                        <span>定位服务器</span>
                        <ArrowRight className="w-3 h-3" />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Unavailable Servers Card */}
            {unavailableServers.length > 0 && (
              <div className="bg-red-50/70 border border-red-200 rounded-lg p-4 space-y-3">
                <div className="flex items-start gap-2.5">
                  <ServerOff className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                  <div className="flex-1 min-w-0">
                    <h3 className="text-xs font-semibold text-red-900">
                      A2S 不可用目标服务器 ({unavailableServers.length} 台)
                    </h3>
                    <p className="text-xs text-red-800 mt-0.5 leading-relaxed">
                      无法获取 A2S 状态查询响应，请检查服务器网络路由、UDP 27015 端口开放状态及防火墙规则。
                    </p>
                  </div>
                </div>

                <div className="space-y-1.5 pt-1">
                  {unavailableServers.map((server) => (
                    <div
                      key={server.id}
                      className="bg-white/90 border border-red-200 rounded p-2.5 flex items-center justify-between text-xs"
                    >
                      <div className="min-w-0 pr-2">
                        <div className="font-semibold text-slate-800 truncate">
                          {server.name || server.endpoint}
                        </div>
                        <div className="text-[11px] text-slate-500 font-mono mt-0.5">
                          {server.endpoint}
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() => onNavigateToTarget(server.id)}
                        className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-red-900 bg-red-100 hover:bg-red-200 rounded border border-red-300 transition-colors shrink-0"
                      >
                        <span>查看配置</span>
                        <ArrowRight className="w-3 h-3" />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
        <div className="px-4 py-3 border-b border-slate-200 bg-slate-50/70 flex flex-col xl:flex-row xl:items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <Server className="w-4 h-4 text-blue-600" />
              <h2 className="text-sm font-semibold text-slate-900">暖服调度层级监控</h2>
            </div>
            <span className="text-xs text-slate-500 font-mono">({filteredGroupedServers.length} 台服务器 / {attempts.length} 个暖服任务)</span>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <div className="inline-flex items-center bg-slate-200/80 p-0.5 rounded-md text-xs" aria-label="服务器筛选">
              {([
                ['all', `全部服务器 (${groupedServers.length})`],
                ['active', `正在暖服 (${groupedServers.filter((group) => group.isServerActiveWarmup).length})`],
                ['uncertain', `结果未确认 (${groupedServers.filter((group) => group.isServerUncertain).length})`],
              ] as const).map(([value, label]) => (
                <button key={value} type="button" onClick={() => setFilterType(value)} className={`px-2.5 py-1 rounded font-medium transition-colors ${filterType === value ? 'bg-white text-slate-900 shadow-2xs' : 'text-slate-600 hover:text-slate-900'}`}>{label}</button>
              ))}
            </div>
            <label className="relative">
              <span className="sr-only">搜索服务器、节点或大厅</span>
              <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none" />
              <input type="search" value={searchQuery} onChange={(event) => setSearchQuery(event.target.value)} placeholder="搜索服务器 / 节点 / 大厅" className="pl-8 pr-2.5 py-1 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-1 focus:ring-blue-500 w-48" />
            </label>
            <button type="button" onClick={toggleAllServers} className="inline-flex items-center gap-1 px-2.5 py-1 text-xs text-slate-600 hover:text-slate-900 bg-white border border-slate-200 rounded-md shadow-2xs transition-colors">
              <ChevronsUpDown className="w-3.5 h-3.5" />
              <span>{expandedServerIds.size === groupedServers.length ? '全部折叠' : '全部展开'}</span>
            </button>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1040px] text-left text-xs border-collapse">
            <thead><tr className="bg-slate-100/90 text-slate-600 border-b border-slate-200 font-semibold select-none"><th className="py-2.5 px-3 w-10" /><th className="py-2.5 px-3">目标服务器</th><th className="py-2.5 px-3">A2S 状态 / 在线人数</th><th className="py-2.5 px-3">关联暖服节点数</th><th className="py-2.5 px-3">服务器暖服剩余时间</th><th className="py-2.5 px-3">调度设置</th><th className="py-2.5 px-4 text-right">操作</th></tr></thead>
            <tbody className="divide-y divide-slate-200 text-slate-700">
              {filteredGroupedServers.length === 0 ? <tr><td colSpan={7} className="py-10 text-center text-slate-400">未找到匹配的目标服务器或暖服记录</td></tr> : filteredGroupedServers.map((group) => {
                const { server, attempts: serverAttempts, activeAttempts, uncertainAttempts, serverRemainingSeconds } = group;
                const isExpanded = expandedServerIds.has(server.id);
                const a2sInfo = formatA2sStatus(server.a2sStatus);
                const isActive = group.isServerActiveWarmup;
                const isUncertain = group.isServerUncertain;
                const attemptWindow = server.attemptWindowSeconds || 720;
                const progress = Math.min(100, Math.max(0, (serverRemainingSeconds / attemptWindow) * 100));
                return <React.Fragment key={server.id}>
                  <tr className={`cursor-pointer select-none transition-colors ${isExpanded ? 'bg-blue-50/20 hover:bg-blue-50/35' : 'hover:bg-slate-50/90'} ${isActive ? 'border-l-4 border-l-blue-600' : isUncertain ? 'border-l-4 border-l-amber-500' : 'border-l-4 border-l-transparent'}`} onClick={() => toggleServerExpand(server.id)}>
                    <td className="py-3 px-3 text-center"><button type="button" aria-label={isExpanded ? '折叠节点列表' : '展开节点列表'} onClick={(event) => { event.stopPropagation(); toggleServerExpand(server.id); }} className="p-1 text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 rounded transition-colors">{isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}</button></td>
                    <td className="py-3 px-3"><div className="flex items-center gap-2"><span className="font-semibold text-slate-900 text-sm">{server.name || server.endpoint}</span>{!server.enabled && <span className="text-[10px] text-slate-500 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200">调度已禁用</span>}</div><div className="text-[11px] text-slate-500 font-mono mt-0.5">{server.endpoint}</div></td>
                    <td className="py-3 px-3 whitespace-nowrap"><div className="flex items-center gap-2"><Badge label={a2sInfo.label} badgeClass={a2sInfo.badgeClass} dotClass={a2sInfo.dotClass} /><span className="flex items-center gap-1 font-mono text-slate-700 font-medium"><Users className="w-3.5 h-3.5 text-slate-400" />{server.maxPlayers > 0 ? `${server.currentPlayers} / ${server.maxPlayers}` : '--'}</span></div></td>
                    <td className="py-3 px-3 whitespace-nowrap">{isActive ? <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-800 border border-blue-200"><span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse" />{activeAttempts.length} 个节点正在暖服</span> : isUncertain ? <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-900 border border-amber-300"><AlertTriangle className="w-3 h-3" />{uncertainAttempts.length} 结果未确认</span> : <span className="text-slate-400">{serverAttempts.length > 0 ? `已结束 (${serverAttempts.length} 项)` : '无关联暖服任务'}</span>}</td>
                    <td className="py-3 px-3 whitespace-nowrap">{isActive ? <div className="space-y-1"><div className="flex items-center gap-1.5 font-mono text-sm font-bold text-blue-700"><Clock className="w-3.5 h-3.5 text-blue-600" />{formatSecondsToTime(serverRemainingSeconds)}<span className="text-[11px] font-normal text-slate-400">/ {formatSecondsToTime(attemptWindow)}</span></div><div className="w-32 h-1.5 bg-slate-200 rounded-full overflow-hidden"><div className="h-full bg-blue-600 rounded-full" style={{ width: `${progress}%` }} /></div></div> : isUncertain ? <span className="text-amber-700 font-mono font-medium">00:00 (超时未确认)</span> : <span className="text-slate-400 font-mono">未在暖服</span>}</td>
                    <td className="py-3 px-3 text-slate-600 text-[11px] whitespace-nowrap"><div>目标人数: <strong className="text-slate-800">{server.playerTarget || '--'} 人</strong></div><div className="text-slate-400 mt-0.5">优先级: {server.priority} · {server.requiresReservation ? '需要预留 (并发 1)' : `最大并发 ${server.maxConcurrentWarmups}`}</div></td>
                    <td className="py-3 px-4 text-right whitespace-nowrap"><button type="button" onClick={(event) => { event.stopPropagation(); onNavigateToTarget(server.id); }} className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-blue-700 hover:text-blue-900 bg-blue-50 hover:bg-blue-100 rounded border border-blue-200 transition-colors">定位服务器 <ExternalLink className="w-3 h-3" /></button></td>
                  </tr>
                  {isExpanded && <tr><td colSpan={7} className="p-0 bg-slate-50/70"><div className="border-y border-slate-200 py-3 px-4 pl-10 space-y-2"><div className="flex items-center justify-between text-xs text-slate-500 font-medium pb-1.5 border-b border-slate-200/80"><span className="flex items-center gap-1.5 text-slate-700 font-semibold"><CornerDownRight className="w-3.5 h-3.5 text-slate-400" />分配至该服务器的暖服节点与任务 ({serverAttempts.length})</span><span className="text-[11px] text-slate-400">任务剩余时间共享该服务器暖服周期</span></div>{serverAttempts.length === 0 ? <div className="py-4 text-center text-slate-400">当前服务器暂无暖服任务分配记录</div> : <div className="overflow-x-auto"><table className="w-full min-w-[840px] text-left text-xs border-collapse bg-white border border-slate-200"><thead><tr className="bg-slate-100/80 text-slate-600 border-b border-slate-200"><th className="py-2 px-3">暖服节点</th><th className="py-2 px-3">运行模式</th><th className="py-2 px-3">任务状态</th><th className="py-2 px-3">执行阶段</th><th className="py-2 px-3">大厅 ID</th><th className="py-2 px-3">节点开始时间</th><th className="py-2 px-3">详情 / 异常说明</th></tr></thead><tbody className="divide-y divide-slate-100">{serverAttempts.map((attempt) => { const statusInfo = formatAttemptStatus(attempt.status); const agent = group.associatedAgents.find((item) => item.id === attempt.agentId); return <tr key={attempt.id} className="hover:bg-slate-50/90"><td className="py-2 px-3 font-mono"><div className="flex items-center gap-1.5"><Cpu className="w-3.5 h-3.5 text-blue-600" /><strong className="text-slate-900">{attempt.agentName}</strong></div>{agent?.steamRegion && <div className="text-[10px] text-slate-400 mt-0.5">Steam 区域: {agent.steamRegion}</div>}</td><td className="py-2 px-3 whitespace-nowrap">{formatOperationMode(attempt.operationMode)}</td><td className="py-2 px-3 whitespace-nowrap"><Badge label={statusInfo.label} badgeClass={statusInfo.badgeClass} dotClass={statusInfo.dotClass} /></td><td className="py-2 px-3 whitespace-nowrap"><span className="bg-slate-100 px-1.5 py-0.5 rounded text-[11px] border border-slate-200">{formatAttemptPhase(attempt.phase)}</span></td><td className="py-2 px-3 font-mono whitespace-nowrap">{attempt.lobbyId ? <Tooltip content={`完整大厅 ID: ${attempt.lobbyId}`}><span className="bg-slate-50 px-1.5 py-0.5 rounded text-[11px] text-slate-800 border border-slate-200">{attempt.lobbyId}</span></Tooltip> : <span className="text-slate-400">--</span>}</td><td className="py-2 px-3 font-mono text-[11px] text-slate-500 whitespace-nowrap">{formatDateTime(attempt.startedAt)}</td><td className="py-2 px-3 text-[11px]">{attempt.errorMessage ? <span className="text-red-700 font-medium flex items-center gap-1"><AlertTriangle className="w-3 h-3" />{attempt.errorMessage}</span> : attempt.status === 'active' ? <span className="text-blue-700">正在引导玩家流入匹配</span> : <span className="text-slate-400">正常完成调度</span>}</td></tr>; })}</tbody></table></div>}</div></td></tr>}
                </React.Fragment>;
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
