import React, { useState, useMemo } from 'react';
import {
  TargetServer,
  WarmupAgent,
  WarmupAttempt,
  ServerObservation,
  SteamDownloadRegion,
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
import { formatSteamDownloadRegion } from '../services/steam-download-region-display';
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
  Search,
  ChevronsUpDown,
  CornerDownRight,
  Users,
} from 'lucide-react';

interface OverviewViewProps {
  targets: TargetServer[];
  agents: WarmupAgent[];
  attempts: WarmupAttempt[];
  observations: ServerObservation[];
  steamRegions?: readonly SteamDownloadRegion[];
  onNavigateToTarget: (targetId: string) => void;
  onNavigateTab: (tab: TabKey) => void;
}

type ServerFilterType = 'all' | 'active' | 'uncertain' | 'unavailable';

export const OverviewView: React.FC<OverviewViewProps> = ({
  targets,
  agents,
  attempts,
  onNavigateToTarget,
  onNavigateTab,
  steamRegions = [],
}) => {
  // Summary counts
  const activeAttempts = attempts.filter((a) => a.status === 'active');
  const uncertainAttempts = attempts.filter((a) => a.status === 'uncertain');
  const unavailableServers = targets.filter((t) => t.a2sStatus === 'unavailable');
  const onlineServers = targets.filter((t) => t.a2sStatus === 'online');
  const runningAgents = agents.filter((a) => a.status === 'running');
  const quarantinedAgents = agents.filter((a) => a.status === 'quarantined');

  // Search & Filter state for the Grouped Hierarchical Table
  const [filterType, setFilterType] = useState<ServerFilterType>('all');
  const [searchQuery, setSearchQuery] = useState('');

  // Grouping logic: Organize attempts and agents by Target Server
  const groupedServers = useMemo(() => {
    const serversById = new Map(targets.map((server) => [server.id, server]));
    const serverIds = Array.from(new Set([...targets.map((server) => server.id), ...attempts.map((attempt) => attempt.targetServerId)]));

    return serverIds.map((serverId) => {
      // Find all attempts associated with this server
      const serverAttempts = attempts.filter((a) => a.targetServerId === serverId);
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
      const serverActiveAttempts = serverAttempts.filter((a) => a.status === 'active');
      const serverUncertainAttempts = serverAttempts.filter((a) => a.status === 'uncertain');

      // Server-level remaining time calculation (relative to the target server's warmup session)
      let serverRemainingSeconds = 0;
      let isServerActiveWarmup = false;
      let isServerUncertain = false;

      if (serverActiveAttempts.length > 0) {
        isServerActiveWarmup = true;
        // The server-level remaining time is the active countdown on this server
        serverRemainingSeconds = Math.max(...serverActiveAttempts.map((a) => a.remainingSeconds));
      } else if (serverUncertainAttempts.length > 0) {
        isServerUncertain = true;
        serverRemainingSeconds = 0;
      }

      // Unique agents currently dispatched to this server
      const uniqueAgentIds = Array.from(new Set(serverAttempts.map((a) => a.agentId)));
      const associatedAgents = uniqueAgentIds
        .map((id) => agents.find((ag) => ag.id === id))
        .filter((ag): ag is WarmupAgent => Boolean(ag));

      return {
        server,
        attempts: serverAttempts,
        activeAttempts: serverActiveAttempts,
        uncertainAttempts: serverUncertainAttempts,
        associatedAgents,
        serverRemainingSeconds,
        isServerActiveWarmup,
        isServerUncertain,
      };
    });
  }, [targets, attempts, agents]);

  // Expanded server IDs set: default expand all servers that have active or uncertain attempts
  const [expandedServerIds, setExpandedServerIds] = useState<Set<string>>(() => {
    const defaultExpanded = new Set<string>();
    groupedServers.forEach((g) => {
      if (g.isServerActiveWarmup || g.isServerUncertain) {
        defaultExpanded.add(g.server.id);
      }
    });
    // If no active, expand first server by default
    if (defaultExpanded.size === 0 && groupedServers.length > 0) {
      defaultExpanded.add(groupedServers[0].server.id);
    }
    return defaultExpanded;
  });

  const toggleServerExpand = (serverId: string) => {
    setExpandedServerIds((prev) => {
      const next = new Set(prev);
      if (next.has(serverId)) {
        next.delete(serverId);
      } else {
        next.add(serverId);
      }
      return next;
    });
  };

  const handleToggleExpandAll = () => {
    if (expandedServerIds.size === groupedServers.length) {
      setExpandedServerIds(new Set());
    } else {
      setExpandedServerIds(new Set(groupedServers.map((g) => g.server.id)));
    }
  };

  // Filter and search grouped servers
  const filteredGroupedServers = useMemo(() => {
    return groupedServers.filter((group) => {
      const { server, attempts, associatedAgents } = group;

      // Filter type check
      if (filterType === 'active' && !group.isServerActiveWarmup) return false;
      if (filterType === 'uncertain' && !group.isServerUncertain) return false;
      if (filterType === 'unavailable' && server.a2sStatus !== 'unavailable') return false;

      // Search query check
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const matchServerName = (server.name || '').toLowerCase().includes(q);
        const matchServerEndpoint = server.endpoint.toLowerCase().includes(q);
        const matchAgent = associatedAgents.some((ag) => ag.name.toLowerCase().includes(q));
        const matchAttemptLobby = attempts.some((at) => (at.lobbyId || '').includes(q));
        if (!matchServerName && !matchServerEndpoint && !matchAgent && !matchAttemptLobby) {
          return false;
        }
      }

      return true;
    });
  }, [groupedServers, filterType, searchQuery]);

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
                      无法获取 A2S 状态查询响应，请检查服务器网络路由、游戏端口开放状态及防火墙规则。
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

      {/* Grouped Hierarchical Data Table (按服务器分组层级暖服数据表) */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
        {/* Table Header Toolbar */}
        <div className="px-4 py-3 border-b border-slate-200 bg-slate-50/70 flex flex-col md:flex-row md:items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <Server className="w-4 h-4 text-blue-600" />
              <h2 className="text-sm font-semibold text-slate-900">
                暖服调度层级监控
              </h2>
            </div>
            <span className="text-xs text-slate-500 font-mono">
              ({filteredGroupedServers.length} 台服务器 / {attempts.length} 个暖服任务)
            </span>
          </div>

          {/* Filters & Controls */}
          <div className="flex items-center gap-2 flex-wrap">
            {/* Filter Tabs */}
            <div className="inline-flex items-center bg-slate-200/80 p-0.5 rounded-md text-xs">
              <button
                type="button"
                onClick={() => setFilterType('all')}
                className={`px-2.5 py-1 rounded font-medium transition-all ${
                  filterType === 'all'
                    ? 'bg-white text-slate-900 shadow-2xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                全部服务器 ({groupedServers.length})
              </button>
              <button
                type="button"
                onClick={() => setFilterType('active')}
                className={`px-2.5 py-1 rounded font-medium transition-all ${
                  filterType === 'active'
                    ? 'bg-white text-blue-700 shadow-2xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                正在暖服 ({groupedServers.filter((g) => g.isServerActiveWarmup).length})
              </button>
              <button
                type="button"
                onClick={() => setFilterType('uncertain')}
                className={`px-2.5 py-1 rounded font-medium transition-all ${
                  filterType === 'uncertain'
                    ? 'bg-white text-amber-700 shadow-2xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                结果未确认 ({groupedServers.filter((g) => g.isServerUncertain).length})
              </button>
            </div>

            {/* Search Input */}
            <div className="relative">
              <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="搜索服务器 / 节点 / 大厅"
                className="pl-8 pr-2.5 py-1 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-1 focus:ring-blue-500 w-44"
              />
            </div>

            {/* Expand / Collapse All Button */}
            <button
              type="button"
              onClick={handleToggleExpandAll}
              className="inline-flex items-center gap-1 px-2.5 py-1 text-xs text-slate-600 hover:text-slate-900 bg-white border border-slate-200 rounded-md shadow-2xs transition-colors"
              title={expandedServerIds.size === groupedServers.length ? '折叠全部' : '展开全部'}
            >
              <ChevronsUpDown className="w-3.5 h-3.5" />
              <span>
                {expandedServerIds.size === groupedServers.length ? '全部折叠' : '全部展开'}
              </span>
            </button>
          </div>
        </div>

        {/* Grouped Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-100/90 text-slate-600 border-b border-slate-200 font-semibold select-none">
                <th className="py-2.5 px-3 w-10 text-center"></th>
                <th className="py-2.5 px-3">目标服务器</th>
                <th className="py-2.5 px-3">A2S 状态 / 在线人数</th>
                <th className="py-2.5 px-3">关联暖服节点数</th>
                <th className="py-2.5 px-3">服务器暖服剩余时间</th>
                <th className="py-2.5 px-3">调度设置</th>
                <th className="py-2.5 px-4 text-right">操作</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-200 text-slate-700">
              {filteredGroupedServers.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-10 text-center text-slate-400 text-xs">
                    未找到匹配的目标服务器或暖服记录
                  </td>
                </tr>
              ) : (
                filteredGroupedServers.map((group) => {
                  const {
                    server,
                    attempts: serverAttempts,
                    activeAttempts: serverActiveAttempts,
                    uncertainAttempts: serverUncertainAttempts,
                    serverRemainingSeconds,
                    isServerActiveWarmup,
                    isServerUncertain,
                  } = group;

                  const isExpanded = expandedServerIds.has(server.id);
                  const a2sInfo = formatA2sStatus(server.a2sStatus);
                  const attemptWindow = server.attemptWindowSeconds || 720;
                  const progressPercent = Math.min(
                    100,
                    Math.max(0, (serverRemainingSeconds / attemptWindow) * 100)
                  );

                  return (
                    <React.Fragment key={server.id}>
                      {/* Parent Row (Target Server) */}
                      <tr
                        className={`transition-colors cursor-pointer select-none ${
                          isExpanded ? 'bg-blue-50/20 hover:bg-blue-50/35' : 'bg-white hover:bg-slate-50/90'
                        } ${isServerActiveWarmup ? 'border-l-4 border-l-blue-600' : isServerUncertain ? 'border-l-4 border-l-amber-500' : 'border-l-4 border-l-transparent'}`}
                        onClick={() => toggleServerExpand(server.id)}
                      >
                        {/* Toggle Icon */}
                        <td className="py-3 px-3 text-center">
                          <button
                            type="button"
                            aria-label={isExpanded ? '折叠节点列表' : '展开节点列表'}
                            className="p-1 text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 rounded transition-colors"
                            onClick={(e) => {
                              e.stopPropagation();
                              toggleServerExpand(server.id);
                            }}
                          >
                            {isExpanded ? (
                              <ChevronDown className="w-4 h-4 text-slate-600" />
                            ) : (
                              <ChevronRight className="w-4 h-4 text-slate-400" />
                            )}
                          </button>
                        </td>

                        {/* Target Server Info */}
                        <td className="py-3 px-3">
                          <div className="flex items-center gap-2">
                            <div className="font-semibold text-slate-900 text-sm">
                              {server.name || server.endpoint}
                            </div>
                            {!server.enabled && (
                              <span className="text-[10px] text-slate-500 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200">
                                调度已禁用
                              </span>
                            )}
                          </div>
                          <div className="text-[11px] text-slate-500 font-mono mt-0.5">
                            {server.endpoint}
                          </div>
                        </td>

                        {/* A2S & Players */}
                        <td className="py-3 px-3 whitespace-nowrap">
                          <div className="flex items-center gap-2">
                            <Badge
                              label={a2sInfo.label}
                              badgeClass={a2sInfo.badgeClass}
                              dotClass={a2sInfo.dotClass}
                            />
                            <div className="flex items-center gap-1 font-mono text-slate-700 font-medium">
                              <Users className="w-3.5 h-3.5 text-slate-400" />
                              <span>
                                {server.currentPlayers} / {server.maxPlayers}
                              </span>
                            </div>
                          </div>
                        </td>

                        {/* Associated Agents Count */}
                        <td className="py-3 px-3 whitespace-nowrap">
                          <div className="flex items-center gap-2">
                            {serverActiveAttempts.length > 0 ? (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-800 border border-blue-200">
                                <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse" />
                                {serverActiveAttempts.length} 个节点正在暖服
                              </span>
                            ) : serverUncertainAttempts.length > 0 ? (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-900 border border-amber-300">
                                <AlertTriangle className="w-3 h-3 text-amber-700" />
                                {serverUncertainAttempts.length} 结果未确认
                              </span>
                            ) : (
                              <span className="text-slate-400 text-xs">
                                {serverAttempts.length > 0
                                  ? `空闲 (${serverAttempts.length} 项历史记录)`
                                  : '无关联暖服任务'}
                              </span>
                            )}
                          </div>
                        </td>

                        {/* Server-level Warm-up Remaining Time (服务器暖服剩余时间) */}
                        <td className="py-3 px-3 whitespace-nowrap">
                          {isServerActiveWarmup ? (
                            <div className="space-y-1">
                              <div className="flex items-center gap-1.5 font-mono text-sm font-bold text-blue-700">
                                <Clock className="w-3.5 h-3.5 text-blue-600 animate-pulse" />
                                <span>{formatSecondsToTime(serverRemainingSeconds)}</span>
                                <span className="text-[11px] font-normal text-slate-400">
                                  / {formatSecondsToTime(attemptWindow)}
                                </span>
                              </div>
                              {/* Progress bar */}
                              <div className="w-32 h-1.5 bg-slate-200 rounded-full overflow-hidden">
                                <div
                                  className="h-full bg-blue-600 rounded-full transition-all duration-1000"
                                  style={{ width: `${progressPercent}%` }}
                                />
                              </div>
                            </div>
                          ) : isServerUncertain ? (
                            <div className="flex items-center gap-1 text-amber-700 font-mono font-medium">
                              <AlertTriangle className="w-3.5 h-3.5 text-amber-600" />
                              <span>00:00 (超时未确认)</span>
                            </div>
                          ) : (
                            <span className="text-slate-400 font-mono text-xs">
                              未在暖服
                            </span>
                          )}
                        </td>

                        {/* Scheduling Settings */}
                        <td className="py-3 px-3 text-slate-600 text-[11px] whitespace-nowrap">
                          <div>
                            目标人数: <span className="font-semibold text-slate-800">{server.playerTarget} 人</span>
                          </div>
                          <div className="text-slate-400 mt-0.5">
                            优先级: {server.priority} · {server.requiresReservation ? '需要预留 (并发1)' : `最大并发 ${server.maxConcurrentWarmups}`}
                          </div>
                        </td>

                        {/* Actions */}
                        <td className="py-3 px-4 text-right whitespace-nowrap">
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              onNavigateToTarget(server.id);
                            }}
                            className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-blue-700 hover:text-blue-900 bg-blue-50 hover:bg-blue-100 rounded border border-blue-200 transition-colors"
                            title="前往目标服务器管理"
                          >
                            <span>定位服务器</span>
                            <ExternalLink className="w-3 h-3" />
                          </button>
                        </td>
                      </tr>

                      {/* Child Rows (Sub-Table / Hierarchical Warm-up Nodes & Attempts) */}
                      {isExpanded && (
                        <tr>
                          <td colSpan={7} className="p-0 bg-slate-50/70">
                            <div className="border-y border-slate-200 py-3 px-4 pl-10 space-y-2">
                              <div className="flex items-center justify-between text-xs text-slate-500 font-medium pb-1.5 border-b border-slate-200/80">
                                <div className="flex items-center gap-1.5">
                                  <CornerDownRight className="w-3.5 h-3.5 text-slate-400" />
                                  <span className="text-slate-700 font-semibold">
                                    分配至该服务器的暖服节点与任务 ({serverAttempts.length})
                                  </span>
                                </div>
                                <span className="text-[11px] text-slate-400">
                                  任务剩余时间共享该服务器暖服周期
                                </span>
                              </div>

                              {serverAttempts.length === 0 ? (
                                <div className="py-4 text-center text-xs text-slate-400">
                                  当前服务器暂无活跃或历史暖服任务分配记录
                                </div>
                              ) : (
                                <div className="overflow-x-auto">
                                  <table className="w-full text-left text-xs border-collapse bg-white rounded border border-slate-200">
                                    <thead>
                                      <tr className="bg-slate-100/80 text-slate-600 border-b border-slate-200">
                                        <th className="py-2 px-3 font-semibold">暖服节点</th>
                                        <th className="py-2 px-3 font-semibold">运行模式</th>
                                        <th className="py-2 px-3 font-semibold">任务状态</th>
                                        <th className="py-2 px-3 font-semibold">执行阶段</th>
                                        <th className="py-2 px-3 font-semibold">大厅 ID</th>
                                        <th className="py-2 px-3 font-semibold">服务器暖服开始时间</th>
                                        <th className="py-2 px-3 font-semibold">详情 / 异常说明</th>
                                      </tr>
                                    </thead>
                                    <tbody className="divide-y divide-slate-100 text-slate-700">
                                      {serverAttempts.map((attempt) => {
                                        const attemptStatusInfo = formatAttemptStatus(attempt.status);
                                        const agentObj = agents.find((ag) => ag.id === attempt.agentId);

                                        return (
                                          <tr
                                            key={attempt.id}
                                            className="hover:bg-slate-50/90 transition-colors"
                                          >
                                            {/* Node Info */}
                                            <td className="py-2 px-3 font-mono">
                                              <div className="flex items-center gap-1.5">
                                                <Cpu className="w-3.5 h-3.5 text-blue-600 shrink-0" />
                                                <span className="font-semibold text-slate-900">
                                                  {attempt.agentName}
                                                </span>
                                                <span className="bg-slate-100 px-1.5 py-0.5 rounded text-[11px] border border-slate-200">
                                                  {formatSteamDownloadRegion(agentObj?.steamRegion, steamRegions)}
                                                </span>
                                              </div>
                                            </td>

                                            {/* Operation Mode */}
                                            <td className="py-2 px-3 whitespace-nowrap text-slate-600">
                                              {formatOperationMode(attempt.operationMode)}
                                            </td>

                                            {/* Attempt Status */}
                                            <td className="py-2 px-3 whitespace-nowrap">
                                              <Badge
                                                label={attemptStatusInfo.label}
                                                badgeClass={attemptStatusInfo.badgeClass}
                                                dotClass={attemptStatusInfo.dotClass}
                                              />
                                            </td>

                                            {/* Phase */}
                                            <td className="py-2 px-3 whitespace-nowrap text-slate-700 font-medium">
                                              <span className="bg-slate-100 px-1.5 py-0.5 rounded text-[11px] border border-slate-200">
                                                {formatAttemptPhase(attempt.phase)}
                                              </span>
                                            </td>

                                            {/* Lobby ID */}
                                            <td className="py-2 px-3 font-mono whitespace-nowrap">
                                              {attempt.lobbyId ? (
                                                <Tooltip content={`完整大厅 ID: ${attempt.lobbyId}`}>
                                                  <span className="bg-slate-100 px-1.5 py-0.5 rounded text-[11px] border border-slate-200">
                                                    {attempt.lobbyId}
                                                  </span>
                                                </Tooltip>
                                              ) : (
                                                <span className="text-slate-400">--</span>
                                              )}
                                            </td>

                                            {/* Started At */}
                                            <td className="py-2 px-3 font-mono text-[11px] text-slate-500 whitespace-nowrap">
                                              {formatDateTime(attempt.startedAt)}
                                            </td>

                                            {/* Message / Error */}
                                            <td className="py-2 px-3 text-slate-500 text-[11px]">
                                              {attempt.errorMessage ? (
                                                <span className="text-red-700 font-medium flex items-center gap-1">
                                                  <AlertTriangle className="w-3 h-3 text-red-600 shrink-0" />
                                                  {attempt.errorMessage}
                                                </span>
                                              ) : attempt.status === 'active' ? (
                                                <span className="text-blue-700">正在引导玩家流入匹配</span>
                                              ) : (
                                                <span className="text-slate-400">正常完成调度</span>
                                              )}
                                            </td>
                                          </tr>
                                        );
                                      })}
                                    </tbody>
                                  </table>
                                </div>
                              )}
                            </div>
                          </td>
                        </tr>
                      )}
                    </React.Fragment>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
