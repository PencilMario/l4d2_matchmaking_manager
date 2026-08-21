import React, { useState, useMemo } from 'react';
import { TargetServer, WarmupAttempt } from '../types';
import { Badge } from './common/Badge';
import { Tooltip } from './common/Tooltip';
import { TargetServerDrawer } from './TargetServerDrawer';
import { TargetServerModal } from './TargetServerModal';
import { ConfirmDialog } from './common/ConfirmDialog';
import {
  formatA2sStatus,
  formatDateTime,
} from '../utils/statusMapping';
import {
  Plus,
  Search,
  RotateCw,
  SlidersHorizontal,
  Power,
  Trash2,
  Edit3,
  Users,
  Eye,
} from 'lucide-react';

interface TargetServersViewProps {
  targets: TargetServer[];
  attempts: WarmupAttempt[];
  onRefresh: () => void;
  onCreateTarget: (data: any) => Promise<boolean>;
  onUpdateTarget: (id: string, data: any) => Promise<boolean>;
  onDeleteTarget: (id: string) => Promise<{ success: boolean; error?: string }>;
  onToggleTargetEnabled: (id: string, enabled: boolean) => Promise<{ success: boolean; error?: string }>;
  onModalOpenChange?: (open: boolean) => void;
  selectedServerId?: string | null;
  onClearSelectedServer?: () => void;
  isRefreshing?: boolean;
}

export const TargetServersView: React.FC<TargetServersViewProps> = ({
  targets,
  attempts,
  onRefresh,
  onCreateTarget,
  onUpdateTarget,
  onDeleteTarget,
  onToggleTargetEnabled,
  onModalOpenChange,
  selectedServerId,
  onClearSelectedServer,
  isRefreshing = false,
}) => {
  // Search and Filter States
  const [searchTerm, setSearchTerm] = useState('');
  const [a2sFilter, setA2sFilter] = useState<string>('all'); // all, online, unavailable, pending
  const [enabledFilter, setEnabledFilter] = useState<string>('all'); // all, enabled, disabled

  // Modals and Drawers
  const [activeDrawerServer, setActiveDrawerServer] = useState<TargetServer | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editServerData, setEditServerData] = useState<TargetServer | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Confirm Dialogs
  const [toggleConfirmTarget, setToggleConfirmTarget] = useState<TargetServer | null>(null);
  const [deleteConfirmTarget, setDeleteConfirmTarget] = useState<TargetServer | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [drawerErrorMessage, setDrawerErrorMessage] = useState<string | null>(null);

  React.useEffect(() => {
    onModalOpenChange?.(isCreateModalOpen);
    return () => onModalOpenChange?.(false);
  }, [isCreateModalOpen, onModalOpenChange]);

  // Synchronize incoming selectedServerId if opened from external link
  React.useEffect(() => {
    if (selectedServerId) {
      const found = targets.find((t) => t.id === selectedServerId);
      if (found) {
        setActiveDrawerServer(found);
      }
    }
  }, [selectedServerId, targets]);

  // Keep drawer data up to date on 5-second polling updates
  React.useEffect(() => {
    if (activeDrawerServer) {
      const fresh = targets.find((t) => t.id === activeDrawerServer.id);
      if (fresh) {
        setActiveDrawerServer(fresh);
      }
    }
  }, [targets]);

  // Filtered List
  const filteredTargets = useMemo(() => {
    return targets.filter((server) => {
      // Search
      const matchSearch =
        !searchTerm.trim() ||
        server.endpoint.toLowerCase().includes(searchTerm.toLowerCase().trim()) ||
        (server.name && server.name.toLowerCase().includes(searchTerm.toLowerCase().trim()));

      // A2S Status
      const matchA2s =
        a2sFilter === 'all' ||
        (a2sFilter === 'online' && server.a2sStatus === 'online') ||
        (a2sFilter === 'unavailable' && server.a2sStatus === 'unavailable') ||
        (a2sFilter === 'pending' && server.a2sStatus === 'pending');

      // Schedule status
      const matchEnabled =
        enabledFilter === 'all' ||
        (enabledFilter === 'enabled' && server.enabled) ||
        (enabledFilter === 'disabled' && !server.enabled);

      return matchSearch && matchA2s && matchEnabled;
    });
  }, [targets, searchTerm, a2sFilter, enabledFilter]);

  // Actions
  const handleOpenCreate = () => {
    setEditServerData(null);
    setIsCreateModalOpen(true);
  };

  const handleOpenEdit = (server: TargetServer) => {
    setEditServerData(server);
    setIsCreateModalOpen(true);
  };

  const handleFormSubmit = async (formData: any) => {
    setIsSubmitting(true);
    try {
      if (editServerData) {
        await onUpdateTarget(editServerData.id, formData);
      } else {
        await onCreateTarget(formData);
      }
      setIsCreateModalOpen(false);
      setEditServerData(null);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmToggle = async () => {
    if (!toggleConfirmTarget) return;
    setActionLoading(true);
    setDrawerErrorMessage(null);
    try {
      const nextState = !toggleConfirmTarget.enabled;
      const res = await onToggleTargetEnabled(toggleConfirmTarget.id, nextState);
      if (res.success) {
        setToggleConfirmTarget(null);
      } else if (res.error) {
        // In case of 409 target_server_drain_failed, do NOT close drawer, show explicit message
        setDrawerErrorMessage(res.error);
        setToggleConfirmTarget(null);
      }
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (!deleteConfirmTarget) return;
    setActionLoading(true);
    setDrawerErrorMessage(null);
    try {
      const res = await onDeleteTarget(deleteConfirmTarget.id);
      if (res.success) {
        setDeleteConfirmTarget(null);
        setActiveDrawerServer(null);
      } else if (res.error) {
        setDrawerErrorMessage(res.error);
        setDeleteConfirmTarget(null);
      }
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="space-y-4 pb-12">
      {/* Page Title */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-900">目标服务器</h1>
        <button
          type="button"
          onClick={handleOpenCreate}
          className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-md shadow-xs transition-colors"
        >
          <Plus className="w-4 h-4" />
          <span>新增服务器</span>
        </button>
      </div>

      {/* Toolbar: Search, Filters, Refresh */}
      <div className="bg-white p-3 rounded-lg border border-slate-200 shadow-xs flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3 flex-1 min-w-[280px]">
          {/* Search Box */}
          <div className="relative min-w-[200px] max-w-xs flex-1">
            <div className="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none text-slate-400">
              <Search className="w-3.5 h-3.5" />
            </div>
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="搜索服务器名称或地址..."
              className="w-full pl-8 pr-3 py-1.5 text-xs bg-slate-50 border border-slate-300 rounded-md focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500 transition-colors"
            />
          </div>

          {/* A2S Status Filter */}
          <div className="flex items-center gap-1.5 text-xs">
            <span className="text-slate-500 shrink-0">状态:</span>
            <select
              value={a2sFilter}
              onChange={(e) => setA2sFilter(e.target.value)}
              className="px-2.5 py-1.5 text-xs bg-slate-50 border border-slate-300 rounded-md focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-700"
            >
              <option value="all">全部</option>
              <option value="online">在线</option>
              <option value="unavailable">不可用</option>
              <option value="pending">等待观测</option>
            </select>
          </div>

          {/* Schedule Status Filter */}
          <div className="flex items-center gap-1.5 text-xs">
            <span className="text-slate-500 shrink-0">调度:</span>
            <select
              value={enabledFilter}
              onChange={(e) => setEnabledFilter(e.target.value)}
              className="px-2.5 py-1.5 text-xs bg-slate-50 border border-slate-300 rounded-md focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-700"
            >
              <option value="all">全部</option>
              <option value="enabled">已启用</option>
              <option value="disabled">已禁用</option>
            </select>
          </div>
        </div>

        {/* Refresh button */}
        <button
          type="button"
          onClick={onRefresh}
          disabled={isRefreshing}
          aria-label="手动刷新目标服务器列表"
          className="inline-flex items-center gap-1.5 px-2.5 py-1.5 text-xs text-slate-600 hover:text-slate-900 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-md transition-colors disabled:opacity-50"
        >
          <RotateCw className={`w-3.5 h-3.5 ${isRefreshing ? 'animate-spin text-blue-600' : ''}`} />
          <span>刷新</span>
        </button>
      </div>

      {/* Table Area */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-50/90 text-slate-600 border-b border-slate-200 font-semibold">
                <th className="py-2.5 px-3.5">服务器名称</th>
                <th className="py-2.5 px-3">服务器地址</th>
                <th className="py-2.5 px-3">玩家数/最大</th>
                <th className="py-2.5 px-3">A2S 状态</th>
                <th className="py-2.5 px-3">最后观测</th>
                <th className="py-2.5 px-3">调度状态</th>
                <th className="py-2.5 px-3 text-center">优先级</th>
                <th className="py-2.5 px-3 text-center">最大并发</th>
                <th className="py-2.5 px-3 text-center">进行中暖服</th>
                <th className="py-2.5 px-3 text-right">操作</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-slate-700">
              {filteredTargets.length === 0 ? (
                <tr>
                  <td colSpan={10} className="py-10 text-center text-slate-400 text-xs">
                    没有找到符合条件的目标服务器
                  </td>
                </tr>
              ) : (
                filteredTargets.map((server) => {
                  const a2sInfo = formatA2sStatus(server.a2sStatus);
                  const activeAttemptsForServer = attempts.filter(
                    (a) => a.targetServerId === server.id && (a.status === 'active' || a.status === 'uncertain')
                  ).length;

                  return (
                    <tr
                      key={server.id}
                      onClick={() => setActiveDrawerServer(server)}
                      className="hover:bg-slate-50/90 cursor-pointer transition-colors"
                    >
                      {/* Name */}
                      <td className="py-2.5 px-3.5 max-w-[200px]">
                        <Tooltip content={server.name || server.endpoint}>
                          <div className="font-semibold text-slate-900 truncate">
                            {server.name || server.endpoint}
                          </div>
                        </Tooltip>
                        {server.requiresReservation && (
                          <span className="inline-block mt-0.5 text-[10px] font-medium text-purple-700 bg-purple-50 px-1 rounded border border-purple-200">
                            预留专线
                          </span>
                        )}
                      </td>

                      {/* Endpoint */}
                      <td className="py-2.5 px-3 font-mono text-slate-600 whitespace-nowrap max-w-[150px]">
                        <Tooltip content={`完整地址: ${server.endpoint}`}>
                          <span className="truncate block">{server.endpoint}</span>
                        </Tooltip>
                      </td>

                      {/* Players */}
                      <td className="py-2.5 px-3 whitespace-nowrap">
                        <div className="flex items-center gap-1 font-medium text-slate-800">
                          <Users className="w-3 h-3 text-slate-400" />
                          <span>
                            {server.currentPlayers} / {server.maxPlayers}
                          </span>
                        </div>
                      </td>

                      {/* A2S Status */}
                      <td className="py-2.5 px-3 whitespace-nowrap">
                        <Badge
                          label={a2sInfo.label}
                          badgeClass={a2sInfo.badgeClass}
                          dotClass={a2sInfo.dotClass}
                        />
                      </td>

                      {/* Last Observed */}
                      <td className="py-2.5 px-3 font-mono text-[11px] text-slate-500 whitespace-nowrap">
                        {formatDateTime(server.lastObservedAt)}
                      </td>

                      {/* Enabled Status */}
                      <td className="py-2.5 px-3 whitespace-nowrap">
                        <Badge
                          label={server.enabled ? '已启用' : '已禁用'}
                          badgeClass={
                            server.enabled
                              ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
                              : 'bg-zinc-100 text-zinc-700 border-zinc-300'
                          }
                          dotClass={server.enabled ? 'bg-emerald-600' : 'bg-zinc-500'}
                        />
                      </td>

                      {/* Priority */}
                      <td className="py-2.5 px-3 text-center font-mono font-medium text-slate-800">
                        {server.priority}
                      </td>

                      {/* Max Concurrent */}
                      <td className="py-2.5 px-3 text-center font-mono text-slate-700">
                        {server.maxConcurrentWarmups}
                      </td>

                      {/* Active Warm-ups */}
                      <td className="py-2.5 px-3 text-center whitespace-nowrap">
                        {activeAttemptsForServer > 0 ? (
                          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[11px] font-bold bg-blue-100 text-blue-800">
                            {activeAttemptsForServer}
                          </span>
                        ) : (
                          <span className="text-slate-400">0</span>
                        )}
                      </td>

                      {/* Operations */}
                      <td
                        className="py-2.5 px-3 text-right whitespace-nowrap"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div className="inline-flex items-center gap-1">
                          <Tooltip content="查看详情">
                            <button
                              type="button"
                              onClick={() => setActiveDrawerServer(server)}
                              aria-label="查看服务器详情"
                              className="p-1 text-slate-500 hover:text-blue-600 hover:bg-slate-100 rounded focus:outline-hidden"
                            >
                              <Eye className="w-3.5 h-3.5" />
                            </button>
                          </Tooltip>

                          <Tooltip content="编辑配置">
                            <button
                              type="button"
                              onClick={() => handleOpenEdit(server)}
                              aria-label="编辑服务器配置"
                              className="p-1 text-slate-500 hover:text-blue-600 hover:bg-slate-100 rounded focus:outline-hidden"
                            >
                              <Edit3 className="w-3.5 h-3.5" />
                            </button>
                          </Tooltip>

                          <Tooltip content={server.enabled ? '禁用调度' : '启用调度'}>
                            <button
                              type="button"
                              onClick={() => {
                                if (server.enabled) {
                                  setToggleConfirmTarget(server);
                                } else {
                                  onToggleTargetEnabled(server.id, true);
                                }
                              }}
                              aria-label={server.enabled ? '禁用调度' : '启用调度'}
                              className={`p-1 rounded focus:outline-hidden ${
                                server.enabled
                                  ? 'text-amber-600 hover:bg-amber-50'
                                  : 'text-emerald-600 hover:bg-emerald-50'
                              }`}
                            >
                              <Power className="w-3.5 h-3.5" />
                            </button>
                          </Tooltip>

                          <Tooltip content="删除服务器">
                            <button
                              type="button"
                              onClick={() => setDeleteConfirmTarget(server)}
                              aria-label="删除服务器"
                              className="p-1 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded focus:outline-hidden"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </Tooltip>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Right Drawer for Server Details */}
      <TargetServerDrawer
        isOpen={Boolean(activeDrawerServer)}
        onClose={() => {
          setActiveDrawerServer(null);
          setDrawerErrorMessage(null);
          if (onClearSelectedServer) onClearSelectedServer();
        }}
        server={activeDrawerServer}
        attempts={attempts}
        onEdit={(server) => {
          handleOpenEdit(server);
        }}
        onToggleEnabled={(server) => {
          if (server.enabled) {
            setToggleConfirmTarget(server);
          } else {
            onToggleTargetEnabled(server.id, true);
          }
        }}
        onDelete={(server) => {
          setDeleteConfirmTarget(server);
        }}
        errorMessage={drawerErrorMessage}
      />

      {/* Server Create / Edit Modal */}
      <TargetServerModal
        isOpen={isCreateModalOpen}
        onClose={() => {
          setIsCreateModalOpen(false);
          setEditServerData(null);
        }}
        onSubmit={handleFormSubmit}
        initialData={editServerData}
        isLoading={isSubmitting}
      />

      {/* Disable Confirmation Modal */}
      <ConfirmDialog
        isOpen={Boolean(toggleConfirmTarget)}
        onClose={() => setToggleConfirmTarget(null)}
        onConfirm={handleConfirmToggle}
        title="确认禁用服务器调度？"
        intent="warning"
        confirmLabel="确认禁用"
        isLoading={actionLoading}
        description="控制服务会先停止该服务器上进行中或结果未确认的暖服任务，再保存禁用状态。"
      />

      {/* Delete Confirmation Modal */}
      <ConfirmDialog
        isOpen={Boolean(deleteConfirmTarget)}
        onClose={() => setDeleteConfirmTarget(null)}
        onConfirm={handleConfirmDelete}
        title="确认删除目标服务器？"
        intent="danger"
        confirmLabel="确认删除"
        isLoading={actionLoading}
        description="控制服务会先停止相关暖服任务，确认停止后再删除服务器配置。"
      />
    </div>
  );
};
