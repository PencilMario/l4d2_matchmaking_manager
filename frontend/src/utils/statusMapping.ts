import { A2sStatus, AgentStatus, AttemptPhase, AttemptStatus, OperationMode } from '../types';

export function formatA2sStatus(status?: A2sStatus | string): {
  label: string;
  badgeClass: string;
  dotClass: string;
  code: string;
} {
  switch (status) {
    case 'online':
      return {
        label: '在线',
        badgeClass: 'bg-emerald-50 text-emerald-800 border-emerald-200',
        dotClass: 'bg-emerald-600',
        code: 'online',
      };
    case 'unavailable':
      return {
        label: '不可用',
        badgeClass: 'bg-red-50 text-red-800 border-red-200',
        dotClass: 'bg-red-600',
        code: 'unavailable',
      };
    case 'pending':
      return {
        label: '等待首次观测',
        badgeClass: 'bg-amber-50 text-amber-800 border-amber-200',
        dotClass: 'bg-amber-500',
        code: 'pending',
      };
    case 'stale':
      return {
        label: '数据可能已过期',
        badgeClass: 'bg-zinc-100 text-zinc-700 border-zinc-300',
        dotClass: 'bg-zinc-500',
        code: 'stale',
      };
    default:
      return {
        label: status ? `未知状态 (${status})` : '未知状态',
        badgeClass: 'bg-zinc-100 text-zinc-600 border-zinc-200',
        dotClass: 'bg-zinc-400',
        code: status || 'unknown',
      };
  }
}

export function formatAgentStatus(status?: AgentStatus | string, ready?: boolean): {
  label: string;
  badgeClass: string;
  dotClass: string;
  canStart: boolean;
  canStop: boolean;
  canRebuild: boolean;
  canDelete: boolean;
  canEdit: boolean;
  isQuarantined: boolean;
} {
  switch (status) {
    case 'running':
      if (!ready) {
        return {
          label: '等待 Steam 登录',
          badgeClass: 'bg-amber-50 text-amber-800 border-amber-200',
          dotClass: 'bg-amber-500',
          canStart: false,
          canStop: true,
          canRebuild: true,
          canDelete: true,
          canEdit: true,
          isQuarantined: false,
        };
      }
      return {
        label: '运行中',
        badgeClass: 'bg-emerald-50 text-emerald-800 border-emerald-200',
        dotClass: 'bg-emerald-600',
        canStart: false,
        canStop: true,
        canRebuild: true,
        canDelete: true,
        canEdit: true,
        isQuarantined: false,
      };
    case 'stopped':
      return {
        label: '已停止',
        badgeClass: 'bg-zinc-100 text-zinc-700 border-zinc-300',
        dotClass: 'bg-zinc-500',
        canStart: true,
        canStop: false,
        canRebuild: true,
        canDelete: true,
        canEdit: true,
        isQuarantined: false,
      };
    case 'restarting':
      return {
        label: '正在恢复',
        badgeClass: 'bg-amber-50 text-amber-800 border-amber-200',
        dotClass: 'bg-amber-500',
        canStart: false,
        canStop: false,
        canRebuild: false,
        canDelete: true,
        canEdit: true,
        isQuarantined: false,
      };
    case 'created':
      return {
        label: '已创建',
        badgeClass: 'bg-blue-50 text-blue-800 border-blue-200',
        dotClass: 'bg-blue-600',
        canStart: true,
        canStop: false,
        canRebuild: true,
        canDelete: true,
        canEdit: true,
        isQuarantined: false,
      };
    case 'quarantined':
      return {
        label: '已隔离',
        badgeClass: 'bg-red-50 text-red-800 border-red-200',
        dotClass: 'bg-red-600',
        canStart: false,
        canStop: false,
        canRebuild: false,
        canDelete: true,
        canEdit: true,
        isQuarantined: true,
      };
    default:
      return {
        label: status ? `未知状态 (${status})` : '未知状态',
        badgeClass: 'bg-zinc-100 text-zinc-600 border-zinc-200',
        dotClass: 'bg-zinc-400',
        canStart: false,
        canStop: false,
        canRebuild: false,
        canDelete: true,
        canEdit: true,
        isQuarantined: false,
      };
  }
}

export function formatAttemptStatus(status?: AttemptStatus | string): {
  label: string;
  badgeClass: string;
  dotClass: string;
  isUncertain: boolean;
  isActive: boolean;
} {
  switch (status) {
    case 'active':
      return {
        label: '进行中',
        badgeClass: 'bg-blue-50 text-blue-800 border-blue-200',
        dotClass: 'bg-blue-600 animate-pulse',
        isUncertain: false,
        isActive: true,
      };
    case 'uncertain':
      return {
        label: '结果未确认',
        badgeClass: 'bg-amber-50 text-amber-800 border-amber-300',
        dotClass: 'bg-amber-600',
        isUncertain: true,
        isActive: false,
      };
    case 'completed':
      return {
        label: '已完成',
        badgeClass: 'bg-emerald-50 text-emerald-800 border-emerald-200',
        dotClass: 'bg-emerald-600',
        isUncertain: false,
        isActive: false,
      };
    case 'failed':
      return {
        label: '失败',
        badgeClass: 'bg-red-50 text-red-800 border-red-200',
        dotClass: 'bg-red-600',
        isUncertain: false,
        isActive: false,
      };
    case 'cancelled':
      return {
        label: '已取消',
        badgeClass: 'bg-zinc-100 text-zinc-700 border-zinc-300',
        dotClass: 'bg-zinc-500',
        isUncertain: false,
        isActive: false,
      };
    default:
      return {
        label: status ? `未知状态 (${status})` : '未知状态',
        badgeClass: 'bg-zinc-100 text-zinc-600 border-zinc-200',
        dotClass: 'bg-zinc-400',
        isUncertain: false,
        isActive: false,
      };
  }
}

export function formatAttemptPhase(phase?: AttemptPhase | string): string {
  switch (phase) {
    case 'discovering_lobby':
      return '寻找大厅';
    case 'injecting_match':
      return '注入匹配';
    case 'awaiting_ready':
      return '等待就绪';
    case 'player_inflow':
      return '玩家流入';
    case 'observing_state':
      return '观测验证';
    default:
      return phase ? `未知阶段 (${phase})` : '未开始';
  }
}

export function formatOperationMode(mode?: OperationMode | string): string {
  switch (mode) {
    case 'standard':
      return '标准匹配';
    case 'direct_lobby':
      return '大厅直连';
    case 'reservation_hold':
      return '预留占用';
    default:
      return mode || '标准模式';
  }
}

export function formatSecondsToTime(seconds: number): string {
  if (isNaN(seconds) || seconds < 0) return '00:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
}

export function formatDateTime(isoString?: string): string {
  if (!isoString) return '--';
  try {
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return isoString;
    return d.toLocaleString('zh-CN', {
      hour12: false,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  } catch {
    return isoString;
  }
}

export function formatTimeOnly(date: Date | string | null): string {
  if (!date) return '--:--:--';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '--:--:--';
  return d.toLocaleTimeString('zh-CN', { hour12: false });
}
