import React from 'react';
import { RotateCw, LogOut, CheckCircle2, AlertCircle } from 'lucide-react';
import { Tooltip } from './common/Tooltip';
import { formatTimeOnly } from '../utils/statusMapping';

interface HeaderProps {
  controllerHealthy: boolean;
  lastUpdated: Date | null;
  isRefreshing: boolean;
  onRefresh: () => void;
  onLogout: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  controllerHealthy,
  lastUpdated,
  isRefreshing,
  onRefresh,
  onLogout,
}) => {
  return (
    <header className="h-12 bg-white border-b border-slate-200 px-6 flex items-center justify-between z-20 shrink-0 select-none">
      {/* Left: Service Health Status */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          {controllerHealthy ? (
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-medium bg-emerald-50 text-emerald-800 border border-emerald-200">
              <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 shrink-0" />
              <span>控制服务正常</span>
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-medium bg-red-50 text-red-800 border border-red-200">
              <AlertCircle className="w-3.5 h-3.5 text-red-600 shrink-0 animate-pulse" />
              <span>连接异常</span>
            </span>
          )}
        </div>

        <div className="h-3.5 w-px bg-slate-200" />

        <div className="text-xs text-slate-500 font-mono">
          最后更新于 <span className="text-slate-700 font-semibold">{formatTimeOnly(lastUpdated)}</span>
        </div>
      </div>

      {/* Right: Actions */}
      <div className="flex items-center gap-1.5">
        <Tooltip content="手动刷新数据" position="bottom">
          <button
            type="button"
            onClick={onRefresh}
            disabled={isRefreshing}
            aria-label="手动刷新数据"
            className="p-1.5 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 transition-colors disabled:opacity-50"
          >
            <RotateCw className={`w-4 h-4 ${isRefreshing ? 'animate-spin text-blue-600' : ''}`} />
          </button>
        </Tooltip>

        <div className="h-3.5 w-px bg-slate-200 mx-1" />

        <Tooltip content="退出令牌会话" position="bottom">
          <button
            type="button"
            onClick={onLogout}
            aria-label="退出当前令牌会话"
            className="p-1.5 text-slate-500 hover:text-red-700 hover:bg-red-50 rounded-md focus:outline-hidden focus:ring-2 focus:ring-red-500 transition-colors"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </Tooltip>
      </div>
    </header>
  );
};
