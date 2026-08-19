import React, { useState } from 'react';
import { WarmupAgent, WarmupAttempt } from '../types';
import { Badge } from './common/Badge';
import { Tooltip } from './common/Tooltip';
import { WarmupAgentModal } from './WarmupAgentModal';
import { ConfirmDialog } from './common/ConfirmDialog';
import { formatAgentStatus, formatDateTime } from '../utils/statusMapping';
import {
  Plus,
  RotateCw,
  Play,
  Square,
  RefreshCw,
  Trash2,
  Edit3,
  MonitorUp,
  AlertTriangle,
  Loader2,
  CheckCircle2,
} from 'lucide-react';

interface WarmupAgentsViewProps {
  agents: WarmupAgent[];
  attempts: WarmupAttempt[];
  onRefresh: () => void;
  onCreateAgent: (data: { name: string; steamRegion?: string; keepVncAlive: boolean }) => Promise<boolean>;
  onUpdateAgent: (id: string, data: { name: string; steamRegion?: string; keepVncAlive: boolean }) => Promise<boolean>;
  onStartAgent: (id: string) => Promise<boolean>;
  onStopAgent: (id: string) => Promise<boolean>;
  onRebuildAgent: (id: string) => Promise<{ success: boolean; error?: string }>;
  onOpenVncSession?: (id: string) => Promise<{ url: string; expiresAt: string }>;
  onDeleteAgent: (id: string) => Promise<boolean>;
  isRefreshing?: boolean;
}

export const WarmupAgentsView: React.FC<WarmupAgentsViewProps> = ({
  agents,
  attempts,
  onRefresh,
  onCreateAgent,
  onUpdateAgent,
  onStartAgent,
  onStopAgent,
  onRebuildAgent,
  onOpenVncSession,
  onDeleteAgent,
  isRefreshing = false,
}) => {
  // Modal states
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingAgent, setEditingAgent] = useState<WarmupAgent | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Row-specific rebuild state map: { [agentId]: boolean }
  const [rebuildingAgentIds, setRebuildingAgentIds] = useState<Record<string, boolean>>({});
  const [rebuildConfirmAgent, setRebuildConfirmAgent] = useState<WarmupAgent | null>(null);
  const [rebuildErrors, setRebuildErrors] = useState<Record<string, string>>({});
  const [feedbackMessages, setFeedbackMessages] = useState<Record<string, string>>({});
  const [vncOpeningAgentIds, setVncOpeningAgentIds] = useState<Record<string, boolean>>({});
  const [vncErrors, setVncErrors] = useState<Record<string, string>>({});

  // Delete confirm
  const [deleteConfirmAgent, setDeleteConfirmAgent] = useState<WarmupAgent | null>(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  // Actions
  const handleOpenCreate = () => {
    setEditingAgent(null);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (agent: WarmupAgent) => {
    setEditingAgent(agent);
    setIsModalOpen(true);
  };

  const handleModalSubmit = async (data: { name: string; steamRegion?: string; keepVncAlive: boolean }) => {
    setIsSubmitting(true);
    try {
      if (editingAgent) {
        await onUpdateAgent(editingAgent.id, data);
        if (data.keepVncAlive !== editingAgent.keepVncAlive) {
          const result = await onRebuildAgent(editingAgent.id);
          if (!result.success) throw new Error(result.error || 'VNC 服务设置已保存，但节点重建失败，尚未生效');
        }
      } else {
        await onCreateAgent(data);
      }
      setIsModalOpen(false);
      setEditingAgent(null);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenVnc = async (agent: WarmupAgent) => {
    if (!onOpenVncSession) return;
    const popup = window.open('', '_blank');
    if (!popup) {
      setVncErrors((prev) => ({ ...prev, [agent.id]: '浏览器阻止了 VNC 新窗口，请允许弹窗后重试' }));
      return;
    }

    popup.opener = null;
    setVncOpeningAgentIds((prev) => ({ ...prev, [agent.id]: true }));
    setVncErrors((prev) => {
      const next = { ...prev };
      delete next[agent.id];
      return next;
    });
    try {
      const session = await onOpenVncSession(agent.id);
      popup.location.replace(session.url);
    } catch (err: any) {
      popup.close();
      setVncErrors((prev) => ({ ...prev, [agent.id]: err?.message || '打开 VNC 连接失败' }));
    } finally {
      setVncOpeningAgentIds((prev) => {
        const next = { ...prev };
        delete next[agent.id];
        return next;
      });
    }
  };

  // Trigger Rebuild Confirmation
  const handlePromptRebuild = (agent: WarmupAgent) => {
    setRebuildConfirmAgent(agent);
  };

  // Execute Rebuild Workflow
  const handleConfirmRebuild = async () => {
    if (!rebuildConfirmAgent) return;
    const agentId = rebuildConfirmAgent.id;
    const agentName = rebuildConfirmAgent.name;

    // 3. Confirm and close modal immediately
    setRebuildConfirmAgent(null);

    // Set row loading state
    setRebuildingAgentIds((prev) => ({ ...prev, [agentId]: true }));
    setRebuildErrors((prev) => {
      const next = { ...prev };
      delete next[agentId];
      return next;
    });

    try {
      const result = await onRebuildAgent(agentId);
      if (result.success) {
        setFeedbackMessages((prev) => ({
          ...prev,
          [agentId]: `节点 ${agentName} 已成功重建并恢复调度。`,
        }));
        setTimeout(() => {
          setFeedbackMessages((prev) => {
            const next = { ...prev };
            delete next[agentId];
            return next;
          });
        }, 5000);
      } else {
        setRebuildErrors((prev) => ({
          ...prev,
          [agentId]: result.error || '节点重建请求失败，请稍后重试',
        }));
      }
    } catch (err: any) {
      setRebuildErrors((prev) => ({
        ...prev,
        [agentId]: err?.message || '节点重建异常，请重试',
      }));
    } finally {
      setRebuildingAgentIds((prev) => {
        const next = { ...prev };
        delete next[agentId];
        return next;
      });
    }
  };

  const handleConfirmDelete = async () => {
    if (!deleteConfirmAgent) return;
    setDeleteLoading(true);
    try {
      await onDeleteAgent(deleteConfirmAgent.id);
      setDeleteConfirmAgent(null);
    } finally {
      setDeleteLoading(false);
    }
  };

  return (
    <div className="space-y-4 pb-12">
      {/* Page Title */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-900">暖服节点</h1>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={onRefresh}
            disabled={isRefreshing}
            aria-label="手动刷新暖服节点"
            className="inline-flex items-center gap-1.5 px-3 py-2 text-xs text-slate-600 hover:text-slate-900 bg-white hover:bg-slate-50 border border-slate-200 rounded-md shadow-2xs transition-colors disabled:opacity-50"
          >
            <RotateCw className={`w-3.5 h-3.5 ${isRefreshing ? 'animate-spin text-blue-600' : ''}`} />
            <span>刷新</span>
          </button>

          <button
            type="button"
            onClick={handleOpenCreate}
            className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-md shadow-xs transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>创建节点</span>
          </button>
        </div>
      </div>

      {/* Table Area */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-50/90 text-slate-600 border-b border-slate-200 font-semibold">
                <th className="py-2.5 px-4">节点名称</th>
                <th className="py-2.5 px-3">运行状态</th>
                <th className="py-2.5 px-3">Steam 下载区域</th>
                <th className="py-2.5 px-3">VNC 连接</th>
                <th className="py-2.5 px-3 text-center">关联暖服任务数</th>
                <th className="py-2.5 px-3">创建时间</th>
                <th className="py-2.5 px-3">更新时间</th>
                <th className="py-2.5 px-4 text-right">操作</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-slate-700">
              {agents.length === 0 ? (
                <tr>
                  <td colSpan={8} className="py-10 text-center text-slate-400 text-xs">
                    暂无暖服节点，请点击右上角创建节点
                  </td>
                </tr>
              ) : (
                agents.map((agent) => {
                  const statusInfo = formatAgentStatus(agent.status, agent.ready);
                  const isRebuilding = Boolean(rebuildingAgentIds[agent.id]);
                  const isVncOpening = Boolean(vncOpeningAgentIds[agent.id]);
                  const canOpenVnc = Boolean(onOpenVncSession) && agent.keepVncAlive && agent.status === 'running' && !isRebuilding && !isVncOpening;
                  const vncTooltip = !agent.keepVncAlive
                    ? '请先编辑节点并打开 VNC 服务'
                    : agent.status !== 'running'
                      ? '节点未运行'
                      : isRebuilding
                        ? '节点正在重建'
                        : isVncOpening
                          ? '正在打开 VNC 连接'
                          : '打开 VNC 连接';
                  const activeAttemptsForAgent = attempts.filter(
                    (a) => a.agentId === agent.id && (a.status === 'active' || a.status === 'uncertain')
                  ).length;
                  const rowRebuildError = rebuildErrors[agent.id];
                  const rowVncError = vncErrors[agent.id];
                  const rowFeedback = feedbackMessages[agent.id];

                  return (
                    <React.Fragment key={agent.id}>
                      <tr className={`hover:bg-slate-50/90 transition-colors ${isRebuilding ? 'bg-blue-50/30' : ''}`}>
                        {/* Agent Name */}
                        <td className="py-3 px-4 font-mono font-medium text-slate-900 whitespace-nowrap">
                          <div className="flex items-center gap-2">
                            <span>{agent.name}</span>
                            {agent.status === 'quarantined' && (
                              <Tooltip content={agent.quarantineReason || '检测到异常，需要人工检查'}>
                                <AlertTriangle className="w-3.5 h-3.5 text-red-500 shrink-0 cursor-help" />
                              </Tooltip>
                            )}
                          </div>
                        </td>

                        {/* Status */}
                        <td className="py-3 px-3 whitespace-nowrap">
                          {isRebuilding ? (
                            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-medium rounded border bg-blue-50 text-blue-800 border-blue-200">
                              <Loader2 className="w-3 h-3 animate-spin text-blue-600" />
                              <span>正在重建节点</span>
                            </span>
                          ) : (
                            <div>
                              <Badge
                                label={statusInfo.label}
                                badgeClass={statusInfo.badgeClass}
                                dotClass={statusInfo.dotClass}
                              />
                              {statusInfo.isQuarantined && (
                                <span className="block text-[10px] text-red-600 font-medium mt-0.5">
                                  需要人工检查
                                </span>
                              )}
                            </div>
                          )}
                        </td>

                        {/* Steam Region */}
                        <td className="py-3 px-3 whitespace-nowrap text-slate-600">
                          {agent.steamRegion ? (
                            <span className="bg-slate-100 px-1.5 py-0.5 rounded text-[11px] font-medium border border-slate-200">
                              {agent.steamRegion}
                            </span>
                          ) : (
                            <span className="text-slate-400">默认</span>
                          )}
                        </td>

                        {/* VNC Connection */}
                        <td className="py-3 px-3 whitespace-nowrap">
                          <Tooltip content={vncTooltip}>
                            <button
                              type="button"
                              disabled={!canOpenVnc}
                              onClick={() => void handleOpenVnc(agent)}
                              aria-label={`打开 VNC 连接 ${agent.name}`}
                              className="inline-flex h-7 w-7 items-center justify-center rounded text-blue-600 hover:bg-blue-50 focus:outline-hidden disabled:text-slate-300 disabled:hover:bg-transparent disabled:opacity-70"
                            >
                              {isVncOpening ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <MonitorUp className="w-3.5 h-3.5" />}
                            </button>
                          </Tooltip>
                        </td>

                        {/* Active Attempts */}
                        <td className="py-3 px-3 text-center whitespace-nowrap">
                          {activeAttemptsForAgent > 0 ? (
                            <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-blue-100 text-blue-800">
                              {activeAttemptsForAgent}
                            </span>
                          ) : (
                            <span className="text-slate-400">0</span>
                          )}
                        </td>

                        {/* Created At */}
                        <td className="py-3 px-3 font-mono text-[11px] text-slate-500 whitespace-nowrap">
                          {formatDateTime(agent.createdAt)}
                        </td>

                        {/* Updated At */}
                        <td className="py-3 px-3 font-mono text-[11px] text-slate-500 whitespace-nowrap">
                          {formatDateTime(agent.updatedAt)}
                        </td>

                        {/* Actions */}
                        <td className="py-3 px-4 text-right whitespace-nowrap">
                          <div className="inline-flex items-center gap-1">
                            {/* Start / Stop */}
                            {statusInfo.canStart && (
                              <Tooltip content="启动节点">
                                <button
                                  type="button"
                                  disabled={isRebuilding}
                                  onClick={() => onStartAgent(agent.id)}
                                  aria-label="启动暖服节点"
                                  className="p-1 text-emerald-600 hover:bg-emerald-50 rounded focus:outline-hidden disabled:opacity-40"
                                >
                                  <Play className="w-3.5 h-3.5" />
                                </button>
                              </Tooltip>
                            )}

                            {statusInfo.canStop && (
                              <Tooltip content="停止节点">
                                <button
                                  type="button"
                                  disabled={isRebuilding}
                                  onClick={() => onStopAgent(agent.id)}
                                  aria-label="停止暖服节点"
                                  className="p-1 text-amber-600 hover:bg-amber-50 rounded focus:outline-hidden disabled:opacity-40"
                                >
                                  <Square className="w-3.5 h-3.5" />
                                </button>
                              </Tooltip>
                            )}

                            {/* Rebuild */}
                            <Tooltip
                              content={
                                statusInfo.isQuarantined
                                  ? '已隔离节点不能直接重建，需要人工检查'
                                  : '重建节点容器（保留登录数据）'
                              }
                            >
                              <button
                                type="button"
                                disabled={isRebuilding || !statusInfo.canRebuild}
                                onClick={() => handlePromptRebuild(agent)}
                                aria-label="重建节点容器"
                                className="p-1 text-blue-600 hover:bg-blue-50 rounded focus:outline-hidden disabled:opacity-40"
                              >
                                <RefreshCw className={`w-3.5 h-3.5 ${isRebuilding ? 'animate-spin' : ''}`} />
                              </button>
                            </Tooltip>

                            {/* Edit */}
                            <Tooltip content="编辑节点">
                              <button
                                type="button"
                                disabled={isRebuilding || !statusInfo.canEdit}
                                onClick={() => handleOpenEdit(agent)}
                                aria-label="编辑暖服节点"
                                className="p-1 text-slate-500 hover:text-blue-600 hover:bg-slate-100 rounded focus:outline-hidden disabled:opacity-40"
                              >
                                <Edit3 className="w-3.5 h-3.5" />
                              </button>
                            </Tooltip>

                            {/* Delete */}
                            <Tooltip content="删除节点">
                              <button
                                type="button"
                                disabled={isRebuilding || !statusInfo.canDelete}
                                onClick={() => setDeleteConfirmAgent(agent)}
                                aria-label="删除暖服节点"
                                className="p-1 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded focus:outline-hidden disabled:opacity-40"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </Tooltip>
                          </div>
                        </td>
                      </tr>

                      {/* Row Rebuild Error message or Feedback */}
                      {(rowRebuildError || rowVncError || rowFeedback) && (
                        <tr className="bg-slate-50">
                          <td colSpan={8} className="py-2 px-4 text-xs">
                            {rowRebuildError && (
                              <div className="flex items-center justify-between text-red-700 bg-red-50 p-2 rounded border border-red-200">
                                <div className="flex items-center gap-2">
                                  <AlertTriangle className="w-3.5 h-3.5 shrink-0" />
                                  <span>{rowRebuildError}</span>
                                </div>
                                <button
                                  type="button"
                                  onClick={() => handlePromptRebuild(agent)}
                                  className="text-xs font-semibold text-red-800 underline hover:text-red-900"
                                >
                                  重试重建
                                </button>
                              </div>
                            )}
                            {rowVncError && (
                              <div className="flex items-center gap-2 text-red-700 bg-red-50 p-2 rounded border border-red-200">
                                <AlertTriangle className="w-3.5 h-3.5 shrink-0" />
                                <span>{rowVncError}</span>
                              </div>
                            )}
                            {rowFeedback && (
                              <div className="flex items-center gap-2 text-emerald-800 bg-emerald-50 p-2 rounded border border-emerald-200">
                                <CheckCircle2 className="w-3.5 h-3.5 shrink-0 text-emerald-600" />
                                <span>{rowFeedback}</span>
                              </div>
                            )}
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

      {/* Agent Create / Edit Modal */}
      <WarmupAgentModal
        isOpen={isModalOpen}
        onClose={() => {
          setIsModalOpen(false);
          setEditingAgent(null);
        }}
        onSubmit={handleModalSubmit}
        initialData={editingAgent}
        isLoading={isSubmitting}
      />

      {/* Rebuild Confirmation Dialog */}
      <ConfirmDialog
        isOpen={Boolean(rebuildConfirmAgent)}
        onClose={() => setRebuildConfirmAgent(null)}
        onConfirm={handleConfirmRebuild}
        title="确认重建暖服节点容器？"
        intent="warning"
        confirmLabel="立即重建"
        description={
          <div className="space-y-1.5">
            <p className="font-medium text-slate-900">
              节点：{rebuildConfirmAgent?.name}
            </p>
            <p className="text-slate-600">
              保留 Steam 登录数据和账号配置卷，只重建容器。重建期间该节点将暂停接收新任务分配。
            </p>
          </div>
        }
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        isOpen={Boolean(deleteConfirmAgent)}
        onClose={() => setDeleteConfirmAgent(null)}
        onConfirm={handleConfirmDelete}
        title="确认删除暖服节点？"
        intent="danger"
        confirmLabel="确认删除"
        isLoading={deleteLoading}
        description={`确定要删除暖服节点 ${deleteConfirmAgent?.name} 吗？删除后该节点的运行环境和配置将被清理。`}
      />
    </div>
  );
};
