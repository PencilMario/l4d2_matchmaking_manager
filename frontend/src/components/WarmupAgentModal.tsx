import React, { useState, useEffect, useRef } from 'react';
import { WarmupAgent } from '../types';
import { Modal } from './common/Modal';
import { AlertCircle, Loader2 } from 'lucide-react';
import { STEAM_DOWNLOAD_REGIONS } from '../data/steamDownloadRegions';

export { STEAM_DOWNLOAD_REGIONS } from '../data/steamDownloadRegions';

interface WarmupAgentModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: { name: string; steamRegion?: string; keepVncAlive: boolean }) => Promise<void>;
  initialData?: WarmupAgent | null;
  isLoading?: boolean;
}

export const WarmupAgentModal: React.FC<WarmupAgentModalProps> = ({
  isOpen,
  onClose,
  onSubmit,
  initialData,
  isLoading = false,
}) => {
  const isEditing = Boolean(initialData);
  const [name, setName] = useState('');
  const [steamRegion, setSteamRegion] = useState('');
  const [keepVncAlive, setKeepVncAlive] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const steamRegionSelectRef = useRef<HTMLSelectElement>(null);
  const initializedAgentIdRef = useRef<string | null>(null);

  useEffect(() => {
    const agentId = initialData?.id ?? null;
    if (initializedAgentIdRef.current === agentId && isOpen) return;

    if (initialData) {
      setName(initialData.name || '');
      setSteamRegion(initialData.steamRegion || '');
      setKeepVncAlive(initialData.keepVncAlive);
    } else {
      setName('');
      setSteamRegion('');
      setKeepVncAlive(false);
    }
    setError(null);
    initializedAgentIdRef.current = isOpen ? agentId : null;
  }, [initialData, isOpen]);

  useEffect(() => {
    const select = steamRegionSelectRef.current;
    if (!select) return;

    const preventWheelSelection = (event: WheelEvent) => event.preventDefault();
    select.addEventListener('wheel', preventWheelSelection, { passive: false });

    return () => select.removeEventListener('wheel', preventWheelSelection);
  }, [isOpen]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('节点名称不能为空');
      return;
    }

    setError(null);
    try {
      await onSubmit({
        name: name.trim(),
        steamRegion: steamRegion.trim() || undefined,
        keepVncAlive,
      });
    } catch (err: any) {
      setError(err?.message || '保存暖服节点配置失败');
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
        disabled={isLoading || !name.trim()}
        className="inline-flex items-center gap-1.5 px-4 py-1.5 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-md shadow-xs focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:opacity-50 transition-colors"
      >
        {isLoading && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
        <span>{isEditing ? '保存修改' : '创建节点'}</span>
      </button>
    </>
  );

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={isEditing ? '编辑暖服节点' : '创建暖服节点'}
      footer={footer}
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-md text-xs text-red-800 flex items-start gap-2">
            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        )}

        {/* Agent Name */}
        <div>
          <label
            htmlFor="agent-name-input"
            className="block text-xs font-medium text-slate-700 mb-1"
          >
            节点名称 <span className="text-red-500">*</span>
          </label>
          <input
            id="agent-name-input"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="例 agent-node-bj-01"
            className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 font-mono"
            required
            autoFocus
          />
          <p className="text-[11px] text-slate-500 mt-1">
            建议使用语义化的节点标识，便于集群监控与任务分配
          </p>
        </div>

        {/* Steam Region */}
        <div>
          <label
            htmlFor="agent-region-select"
            className="block text-xs font-medium text-slate-700 mb-1"
          >
            Steam 下载区域
          </label>
          <select
            id="agent-region-select"
            ref={steamRegionSelectRef}
            value={steamRegion}
            onChange={(e) => setSteamRegion(e.target.value)}
            className="w-full px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-800"
          >
            {STEAM_DOWNLOAD_REGIONS.map((reg) => (
              <option key={reg.value} value={reg.value}>
                {reg.label}
              </option>
            ))}
          </select>
          <p className="text-[11px] text-slate-500 mt-1">
            空值说明：使用 Steam 默认区域。设置匹配就近节点可提升大厅连接成功率。
          </p>
        </div>

        <label className="flex items-start gap-2.5 rounded-md border border-slate-200 bg-slate-50/70 px-3 py-2.5 cursor-pointer">
          <input
            type="checkbox"
            checked={keepVncAlive}
            onChange={(event) => setKeepVncAlive(event.target.checked)}
            className="mt-0.5 h-3.5 w-3.5 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
          />
          <span>
            <span className="block text-xs font-medium text-slate-700">打开 VNC 服务</span>
            <span className="mt-0.5 block text-[11px] leading-4 text-slate-500">
              保存后将重建节点以应用设置；开启后可通过受控连接访问节点桌面。
            </span>
          </span>
        </label>
      </form>
    </Modal>
  );
};
