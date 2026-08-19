import React, { useState } from 'react';
import { TargetServer, WarmupAttempt } from '../types';
import { Drawer } from './common/Drawer';
import { Badge } from './common/Badge';
import { Tooltip } from './common/Tooltip';
import {
  formatA2sStatus,
  formatAttemptPhase,
  formatAttemptStatus,
  formatDateTime,
  formatOperationMode,
  formatSecondsToTime,
} from '../utils/statusMapping';
import {
  Edit3,
  Power,
  Trash2,
  Users,
  Activity,
  Key,
  Clock,
  Radio,
  Sliders,
  AlertTriangle,
} from 'lucide-react';

interface TargetServerDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  server: TargetServer | null;
  attempts: WarmupAttempt[];
  onEdit: (server: TargetServer) => void;
  onToggleEnabled: (server: TargetServer) => void;
  onDelete: (server: TargetServer) => void;
  errorMessage?: string | null;
}

export const TargetServerDrawer: React.FC<TargetServerDrawerProps> = ({
  isOpen,
  onClose,
  server,
  attempts,
  onEdit,
  onToggleEnabled,
  onDelete,
  errorMessage,
}) => {
  if (!server) return null;

  const a2sInfo = formatA2sStatus(server.a2sStatus);
  const relatedAttempts = attempts.filter((a) => a.targetServerId === server.id);

  const footer = (
    <div className="w-full flex items-center justify-between">
      <button
        type="button"
        onClick={() => onDelete(server)}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-red-700 hover:text-red-800 hover:bg-red-50 rounded-md border border-red-200 transition-colors"
      >
        <Trash2 className="w-3.5 h-3.5" />
        <span>删除服务器</span>
      </button>

      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={() => onToggleEnabled(server)}
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md border transition-colors ${
            server.enabled
              ? 'text-amber-800 bg-amber-50 hover:bg-amber-100 border-amber-200'
              : 'text-emerald-800 bg-emerald-50 hover:bg-emerald-100 border-emerald-200'
          }`}
        >
          <Power className="w-3.5 h-3.5" />
          <span>{server.enabled ? '禁用调度' : '启用调度'}</span>
        </button>

        <button
          type="button"
          onClick={() => onEdit(server)}
          className="inline-flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-md shadow-xs transition-colors"
        >
          <Edit3 className="w-3.5 h-3.5" />
          <span>编辑配置</span>
        </button>
      </div>
    </div>
  );

  return (
    <Drawer
      isOpen={isOpen}
      onClose={onClose}
      title={server.name || server.endpoint}
      subtitle={server.endpoint}
      footer={footer}
      width="max-w-2xl"
    >
      {/* 409 Drain failure or other error alert */}
      {errorMessage && (
        <div className="p-3.5 bg-red-50 border border-red-200 rounded-lg text-xs text-red-800 flex items-start gap-2.5">
          <AlertTriangle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
          <div className="leading-relaxed">{errorMessage}</div>
        </div>
      )}

      {/* 1. 实时状态 (Real-time Status) */}
      <div className="space-y-3">
        <div className="flex items-center gap-2 pb-1 border-b border-slate-200">
          <Radio className="w-4 h-4 text-blue-600" />
          <h3 className="text-xs font-bold text-slate-900 uppercase tracking-wide">
            实时状态
          </h3>
        </div>

        <div className="grid grid-cols-2 gap-3 bg-slate-50/80 p-3.5 rounded-lg border border-slate-200 text-xs">
          <div>
            <span className="text-slate-500 block mb-0.5">A2S 状态</span>
            <Badge
              label={a2sInfo.label}
              badgeClass={a2sInfo.badgeClass}
              dotClass={a2sInfo.dotClass}
            />
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">当前玩家</span>
            <div className="flex items-center gap-1.5 text-slate-900 font-medium">
              <Users className="w-3.5 h-3.5 text-slate-500" />
              <span>
                {server.currentPlayers} / {server.maxPlayers} 人
              </span>
            </div>
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">最后观测时间</span>
            <span className="text-slate-800 font-mono text-[11px]">
              {formatDateTime(server.lastObservedAt)}
            </span>
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">服务器地址</span>
            <span className="text-slate-800 font-mono font-medium text-[11px] truncate block" title={server.endpoint}>
              {server.endpoint}
            </span>
          </div>
        </div>
      </div>

      {/* 2. 调度设置 (Schedule Settings) */}
      <div className="space-y-3">
        <div className="flex items-center gap-2 pb-1 border-b border-slate-200">
          <Sliders className="w-4 h-4 text-blue-600" />
          <h3 className="text-xs font-bold text-slate-900 uppercase tracking-wide">
            调度设置
          </h3>
        </div>

        <div className="grid grid-cols-2 gap-y-3 gap-x-4 bg-slate-50/80 p-3.5 rounded-lg border border-slate-200 text-xs">
          <div>
            <span className="text-slate-500 block mb-0.5">调度状态</span>
            <Badge
              label={server.enabled ? '已启用' : '已禁用'}
              badgeClass={
                server.enabled
                  ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
                  : 'bg-zinc-100 text-zinc-700 border-zinc-300'
              }
              dotClass={server.enabled ? 'bg-emerald-600' : 'bg-zinc-500'}
            />
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">需要大厅预留</span>
            <span className="font-medium text-slate-800">
              {server.requiresReservation ? '是 (预留专线)' : '否 (常规匹配)'}
            </span>
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">调度优先级</span>
            <span className="font-mono font-semibold text-slate-800">
              {server.priority}
            </span>
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">最大并发暖服数</span>
            <span className="font-mono text-slate-800">
              {server.maxConcurrentWarmups}
              {server.requiresReservation && ' (预留锁定)'}
            </span>
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">单次暖服时限</span>
            <span className="font-mono text-slate-800">
              {server.attemptWindowSeconds} 秒（
              {(server.attemptWindowSeconds / 60).toFixed(0)} 分钟）
            </span>
          </div>

          <div>
            <span className="text-slate-500 block mb-0.5">目标玩家数</span>
            <span className="font-mono text-slate-800">{server.playerTarget} 人</span>
          </div>

          <div className="col-span-2 pt-1 border-t border-slate-200 flex items-center justify-between">
            <span className="text-slate-500">RCON 凭据状态</span>
            {server.hasRconPassword ? (
              <span className="inline-flex items-center gap-1 text-emerald-700 font-medium bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                <Key className="w-3 h-3 text-emerald-600" />
                <span>已配置</span>
              </span>
            ) : (
              <span className="text-slate-400">未配置</span>
            )}
          </div>
        </div>
      </div>

      {/* 3. 关联暖服任务 (Related Warm-up Attempts) */}
      <div className="space-y-3">
        <div className="flex items-center justify-between pb-1 border-b border-slate-200">
          <div className="flex items-center gap-2">
            <Activity className="w-4 h-4 text-blue-600" />
            <h3 className="text-xs font-bold text-slate-900 uppercase tracking-wide">
              关联暖服任务
            </h3>
          </div>
          <span className="text-xs text-slate-500 font-mono">
            {relatedAttempts.length} 项记录
          </span>
        </div>

        {relatedAttempts.length === 0 ? (
          <div className="p-4 text-center text-xs text-slate-400 bg-slate-50 rounded-lg border border-dashed border-slate-200">
            该服务器当前没有关联的暖服任务记录
          </div>
        ) : (
          <div className="space-y-2">
            {relatedAttempts.map((att) => {
              const statusInfo = formatAttemptStatus(att.status);
              return (
                <div
                  key={att.id}
                  className="p-3 bg-white rounded-lg border border-slate-200 shadow-2xs space-y-1.5 text-xs hover:border-slate-300 transition-colors"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-slate-800">
                        节点: {att.agentName}
                      </span>
                      <span className="text-slate-400">·</span>
                      <span className="text-slate-500">
                        {formatOperationMode(att.operationMode)}
                      </span>
                    </div>
                    <Badge
                      label={statusInfo.label}
                      badgeClass={statusInfo.badgeClass}
                      dotClass={statusInfo.dotClass}
                    />
                  </div>

                  <div className="grid grid-cols-3 gap-2 text-[11px] text-slate-500 pt-1">
                    <div>
                      <span className="text-slate-400">阶段: </span>
                      <span className="text-slate-700 font-medium">
                        {formatAttemptPhase(att.phase)}
                      </span>
                    </div>
                    <div>
                      <span className="text-slate-400">大厅: </span>
                      <span className="font-mono text-slate-700">
                        {att.lobbyId || '--'}
                      </span>
                    </div>
                    <div>
                      <span className="text-slate-400">剩余: </span>
                      <span className="font-mono text-blue-700 font-medium">
                        {att.status === 'active'
                          ? formatSecondsToTime(att.remainingSeconds)
                          : '已结束'}
                      </span>
                    </div>
                  </div>

                  {att.errorMessage && (
                    <div className="text-[11px] text-red-600 bg-red-50 p-1.5 rounded">
                      {att.errorMessage}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </Drawer>
  );
};
