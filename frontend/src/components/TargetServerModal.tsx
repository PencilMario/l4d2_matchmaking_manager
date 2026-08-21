import React, { useState, useEffect } from 'react';
import { TargetServer, TargetServerGameMode } from '../types';
import { Modal } from './common/Modal';
import { Switch } from './common/Switch';
import { AlertCircle, Loader2 } from 'lucide-react';

interface TargetServerModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: {
    endpoint: string;
    name?: string;
    requiresReservation: boolean;
    enabled: boolean;
    priority: number;
    maxConcurrentWarmups: number;
    attemptWindowSeconds: number;
    playerTarget: number;
    gameMode: TargetServerGameMode | null;
    rconPassword?: string;
    clearRconPassword?: boolean;
  }) => Promise<void>;
  initialData?: TargetServer | null;
  isLoading?: boolean;
}

export const TargetServerModal: React.FC<TargetServerModalProps> = ({
  isOpen,
  onClose,
  onSubmit,
  initialData,
  isLoading = false,
}) => {
  const isEditing = Boolean(initialData);

  const [endpoint, setEndpoint] = useState('');
  const [name, setName] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [priority, setPriority] = useState(0);
  const [maxConcurrentWarmups, setMaxConcurrentWarmups] = useState(36);
  const [attemptWindowSeconds, setAttemptWindowSeconds] = useState(720);
  const [playerTarget, setPlayerTarget] = useState(4);
  const [gameMode, setGameMode] = useState<TargetServerGameMode | ''>('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (initialData) {
      setEndpoint(initialData.endpoint || '');
      setName(initialData.name || '');
      setEnabled(Boolean(initialData.enabled));
      setPriority(initialData.priority || 0);
      setMaxConcurrentWarmups(initialData.maxConcurrentWarmups || 36);
      setAttemptWindowSeconds(initialData.attemptWindowSeconds || 720);
      setPlayerTarget(initialData.playerTarget || 4);
      setGameMode(initialData.gameMode ?? '');
    } else {
      setEndpoint('');
      setName('');
      setEnabled(true);
      setPriority(0);
      setMaxConcurrentWarmups(36);
      setAttemptWindowSeconds(720);
      setPlayerTarget(4);
      setGameMode('');
    }
    setError(null);
  }, [initialData, isOpen]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!endpoint.trim()) {
      setError('服务器地址不能为空');
      return;
    }

    setError(null);
    try {
      await onSubmit({
        endpoint: endpoint.trim(),
        name: name.trim() || undefined,
        requiresReservation: false,
        enabled,
        priority: Number(priority) || 0,
        maxConcurrentWarmups: Number(maxConcurrentWarmups) || 36,
        attemptWindowSeconds: Number(attemptWindowSeconds) || 720,
        playerTarget: Number(playerTarget) || 4,
        gameMode: gameMode || null,
      });
    } catch (err: any) {
      setError(err?.message || '保存服务器配置失败');
    }
  };

  const footer = (
    <>
      <button
        type="button"
        onClick={onClose}
        disabled={isLoading}
        className="px-3.5 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 focus:outline-hidden disabled:opacity-50"
      >
        取消
      </button>
      <button
        type="button"
        onClick={handleSubmit}
        disabled={isLoading || !endpoint.trim()}
        className="inline-flex items-center gap-1.5 px-4 py-1.5 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-md shadow-xs focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:opacity-50 transition-colors"
      >
        {isLoading && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
        <span>{isEditing ? '保存修改' : '立即创建'}</span>
      </button>
    </>
  );

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={isEditing ? '编辑目标服务器配置' : '新增目标服务器'}
      footer={footer}
      maxWidth="max-w-xl"
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-md text-xs text-red-800 flex items-start gap-2">
            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        )}

        {/* Endpoint */}
        <div>
          <label
            htmlFor="target-endpoint"
            className="block text-xs font-medium text-slate-700 mb-1"
          >
            服务器地址 <span className="text-red-500">*</span>
          </label>
          <input
            id="target-endpoint"
            type="text"
            value={endpoint}
            onChange={(e) => setEndpoint(e.target.value)}
            placeholder="例 203.0.113.10:27015"
            className="w-full px-3 py-1.5 text-xs font-mono bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500"
            required
          />
          <p className="text-[11px] text-slate-500 mt-1">
            支持主机名或 IPv4 加游戏端口（默认 27015）
          </p>
        </div>

        {/* Lobby Game Mode */}
        <div>
          <label
            htmlFor="target-game-mode"
            className="block text-xs font-medium text-slate-700 mb-1"
          >
            模式类型
          </label>
          <select
            id="target-game-mode"
            value={gameMode}
            onChange={(e) => setGameMode(e.target.value as TargetServerGameMode | '')}
            className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500"
          >
            <option value="">未指定（默认 coop）</option>
            <option value="versus">versus</option>
            <option value="coop">coop</option>
          </select>
          <p className="text-[11px] text-slate-500 mt-1">
            仅可选择预设；未指定时使用已验证的 L4D1 coop 大厅 metadata
          </p>
        </div>

        {/* Server Name */}
        <div>
          <label
            htmlFor="target-name"
            className="block text-xs font-medium text-slate-700 mb-1"
          >
            服务器备注名称
          </label>
          <input
            id="target-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="例 CN-BJ #01 官方战役纯净服"
            className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500"
          />
          <p className="text-[11px] text-slate-500 mt-1">
            若留空，在未获取 A2S 观测时将默认以服务器地址显示
          </p>
        </div>

        {/* Switches */}
        <div className="grid grid-cols-2 gap-4 p-3 bg-slate-50 rounded-lg border border-slate-200">
          <Switch
            id="target-enabled-switch"
            checked={enabled}
            onChange={setEnabled}
            label="启用调度"
            description="允许控制服务为此服务器派发暖服任务"
          />

          <div className="text-xs text-slate-600"><b className="block text-slate-800">L4D1 标准大厅</b><span>预约握手尚未真机验证，当前版本已禁用</span></div>
        </div>

        {/* Priority & Concurrent */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label
              htmlFor="target-priority"
              className="block text-xs font-medium text-slate-700 mb-1"
            >
              调度优先级
            </label>
            <input
              id="target-priority"
              type="number"
              value={priority}
              onChange={(e) => setPriority(parseInt(e.target.value, 10) || 0)}
              className="w-full px-3 py-1.5 text-xs font-mono bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500"
            />
            <p className="text-[11px] text-slate-500 mt-1">
              整数，可为负数，数值越高越优先调度
            </p>
          </div>

          <div>
            <label
              htmlFor="target-max-concurrent"
              className="block text-xs font-medium text-slate-700 mb-1"
            >
              最大并发暖服数
            </label>
            <input
              id="target-max-concurrent"
              type="number"
              min={1}
              max={100}
              value={maxConcurrentWarmups}
              onChange={(e) =>
                setMaxConcurrentWarmups(Math.max(1, parseInt(e.target.value, 10) || 1))
              }
              className="w-full px-3 py-1.5 text-xs font-mono bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:bg-slate-100 disabled:text-slate-500"
            />
            <p className="text-[11px] text-slate-500 mt-1">
              普通服务器默认 36 并发
            </p>
          </div>
        </div>

        {/* Timeout & Target Players */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label
              htmlFor="target-attempt-window"
              className="block text-xs font-medium text-slate-700 mb-1"
            >
              单次暖服时限 (秒)
            </label>
            <input
              id="target-attempt-window"
              type="number"
              min={60}
              step={60}
              value={attemptWindowSeconds}
              onChange={(e) =>
                setAttemptWindowSeconds(Math.max(60, parseInt(e.target.value, 10) || 60))
              }
              className="w-full px-3 py-1.5 text-xs font-mono bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500"
            />
            <p className="text-[11px] text-slate-500 mt-1">
              当前折合：
              <span className="font-semibold text-slate-700">
                {attemptWindowSeconds} 秒（{(attemptWindowSeconds / 60).toFixed(1)} 分钟）
              </span>
            </p>
          </div>

          <div>
            <label
              htmlFor="target-player-target"
              className="block text-xs font-medium text-slate-700 mb-1"
            >
              目标玩家数 (人)
            </label>
            <input
              id="target-player-target"
              type="number"
              min={1}
              max={32}
              value={playerTarget}
              onChange={(e) =>
                setPlayerTarget(Math.max(1, parseInt(e.target.value, 10) || 1))
              }
              className="w-full px-3 py-1.5 text-xs font-mono bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500"
            />
            <p className="text-[11px] text-slate-500 mt-1">
              达到目标人数后自动判定暖服成功
            </p>
          </div>
        </div>

      </form>
    </Modal>
  );
};
