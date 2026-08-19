import React from 'react';
import {
  TargetServer,
  WarmupAgent,
  WarmupAttempt,
  ServerObservation,
  TabKey,
} from '../types';
import {
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

  const summaryCards = [
    {
      label: '正在进行的暖服任务',
      count: activeAttempts.length,
      icon: <Activity className="w-4 h-4 text-blue-600" />,
      bg: 'bg-blue-50/50',
      border: 'border-blue-200',
      textColor: 'text-blue-900',
      onClick: () => {},
    },
    {
      label: '结果未确认的任务',
      count: uncertainAttempts.length,
      icon: <AlertTriangle className="w-4 h-4 text-amber-600" />,
      bg: uncertainAttempts.length > 0 ? 'bg-amber-50' : 'bg-white',
      border: uncertainAttempts.length > 0 ? 'border-amber-300' : 'border-slate-200',
      textColor: uncertainAttempts.length > 0 ? 'text-amber-900 font-bold' : 'text-slate-900',
    },
    {
      label: 'A2S 不可用服务器',
      count: unavailableServers.length,
      icon: <ServerOff className="w-4 h-4 text-red-600" />,
      bg: unavailableServers.length > 0 ? 'bg-red-50' : 'bg-white',
      border: unavailableServers.length > 0 ? 'border-red-300' : 'border-slate-200',
      textColor: unavailableServers.length > 0 ? 'text-red-900 font-bold' : 'text-slate-900',
      onClick: () => onNavigateTab('targets'),
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
            className={`p-3.5 rounded-lg border ${card.border} ${card.bg} shadow-xs flex flex-col justify-between transition-all ${
              card.onClick ? 'cursor-pointer hover:shadow-sm hover:border-slate-300' : ''
            }`}
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

      {/* Warmup Attempts Table (暖服任务表) */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
        <div className="px-5 py-3.5 border-b border-slate-200 bg-slate-50/60 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-semibold text-slate-900">暖服任务</h2>
            <span className="text-xs text-slate-500 font-mono">({attempts.length})</span>
          </div>
          <span className="text-xs text-slate-400">
            按开始时间降序排列
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-50/80 text-slate-600 border-b border-slate-200 font-medium">
                <th className="py-2.5 px-4 font-semibold">目标服务器</th>
                <th className="py-2.5 px-4 font-semibold">暖服节点</th>
                <th className="py-2.5 px-4 font-semibold">运行模式</th>
                <th className="py-2.5 px-4 font-semibold">当前状态</th>
                <th className="py-2.5 px-4 font-semibold">当前阶段</th>
                <th className="py-2.5 px-4 font-semibold">大厅 ID</th>
                <th className="py-2.5 px-4 font-semibold">剩余时间</th>
                <th className="py-2.5 px-4 font-semibold">开始时间</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-slate-700">
              {attempts.length === 0 ? (
                <tr>
                  <td colSpan={8} className="py-8 text-center text-slate-400 text-xs">
                    暂无暖服任务记录
                  </td>
                </tr>
              ) : (
                attempts.map((item) => {
                  const statusInfo = formatAttemptStatus(item.status);
                  const isClickable = Boolean(item.targetServerId);

                  return (
                    <tr
                      key={item.id}
                      className="hover:bg-slate-50/80 transition-colors"
                    >
                      {/* Target Server (Clickable link to target server) */}
                      <td className="py-2.5 px-4 max-w-[220px]">
                        {isClickable ? (
                          <button
                            type="button"
                            onClick={() => onNavigateToTarget(item.targetServerId)}
                            className="text-left font-medium text-blue-600 hover:text-blue-800 hover:underline flex items-center gap-1 group truncate"
                            title={`点击跳转至服务器: ${item.targetServerName || item.targetServerEndpoint}`}
                          >
                            <span className="truncate">
                              {item.targetServerName || item.targetServerEndpoint}
                            </span>
                            <ExternalLink className="w-3 h-3 opacity-0 group-hover:opacity-100 shrink-0 transition-opacity" />
                          </button>
                        ) : (
                          <span className="font-medium text-slate-800 truncate block">
                            {item.targetServerName || item.targetServerEndpoint}
                          </span>
                        )}
                        <span className="text-[11px] text-slate-400 font-mono block truncate">
                          {item.targetServerEndpoint}
                        </span>
                      </td>

                      {/* Agent */}
                      <td className="py-2.5 px-4 font-medium text-slate-800 whitespace-nowrap">
                        {item.agentName}
                      </td>

                      {/* Mode */}
                      <td className="py-2.5 px-4 whitespace-nowrap text-slate-600">
                        {formatOperationMode(item.operationMode)}
                      </td>

                      {/* Status */}
                      <td className="py-2.5 px-4 whitespace-nowrap">
                        <Badge
                          label={statusInfo.label}
                          badgeClass={statusInfo.badgeClass}
                          dotClass={statusInfo.dotClass}
                          title={item.status ? `原始值: ${item.status}` : undefined}
                        />
                      </td>

                      {/* Phase */}
                      <td className="py-2.5 px-4 whitespace-nowrap text-slate-600">
                        {formatAttemptPhase(item.phase)}
                      </td>

                      {/* Lobby ID */}
                      <td className="py-2.5 px-4 font-mono text-slate-600 whitespace-nowrap">
                        {item.lobbyId ? (
                          <Tooltip content={`完整大厅 ID: ${item.lobbyId}`}>
                            <span className="bg-slate-100 px-1.5 py-0.5 rounded text-[11px] text-slate-700 border border-slate-200">
                              {item.lobbyId}
                            </span>
                          </Tooltip>
                        ) : (
                          <span className="text-slate-400">--</span>
                        )}
                      </td>

                      {/* Remaining Time */}
                      <td className="py-2.5 px-4 whitespace-nowrap font-mono text-slate-700">
                        {item.status === 'active' ? (
                          <div className="inline-flex items-center gap-1 font-semibold text-blue-700">
                            <Clock className="w-3 h-3 text-blue-500" />
                            <span>{formatSecondsToTime(item.remainingSeconds)}</span>
                          </div>
                        ) : (
                          <span className="text-slate-400">已结束</span>
                        )}
                      </td>

                      {/* Start Time */}
                      <td className="py-2.5 px-4 whitespace-nowrap text-slate-500 font-mono text-[11px]">
                        {formatDateTime(item.startedAt)}
                      </td>
                    </tr>
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
