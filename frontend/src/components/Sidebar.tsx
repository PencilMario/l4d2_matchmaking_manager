import React from 'react';
import { LayoutDashboard, Server, Cpu, Search, Gamepad2, Settings } from 'lucide-react';
import { TabKey } from '../types';

interface SidebarProps {
  activeTab: TabKey;
  onTabChange: (tab: TabKey) => void;
  uncertainCount: number;
  unavailableCount: number;
  quarantinedCount: number;
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeTab,
  onTabChange,
  uncertainCount,
  unavailableCount,
  quarantinedCount,
}) => {
  const navItems: {
    key: TabKey;
    label: string;
    icon: React.ReactNode;
    badge?: number;
    badgeColor?: string;
  }[] = [
    {
      key: 'overview',
      label: '系统概览',
      icon: <LayoutDashboard className="w-4 h-4 shrink-0" />,
      badge: uncertainCount > 0 ? uncertainCount : undefined,
      badgeColor: 'bg-amber-500 text-slate-900',
    },
    {
      key: 'targets',
      label: '目标服务器',
      icon: <Server className="w-4 h-4 shrink-0" />,
      badge: unavailableCount > 0 ? unavailableCount : undefined,
      badgeColor: 'bg-red-500 text-white',
    },
    {
      key: 'agents',
      label: '暖服节点',
      icon: <Cpu className="w-4 h-4 shrink-0" />,
      badge: quarantinedCount > 0 ? quarantinedCount : undefined,
      badgeColor: 'bg-amber-500 text-slate-900',
    },
    {
      key: 'lobby',
      label: '大厅查询',
      icon: <Search className="w-4 h-4 shrink-0" />,
    },
    {
      key: 'settings',
      label: '全局设置',
      icon: <Settings className="w-4 h-4 shrink-0" />,
    },
  ];

  return (
    <aside className="w-56 bg-[#181b22] text-slate-300 flex flex-col shrink-0 select-none border-r border-slate-800">
      {/* Brand / Logo */}
      <div className="h-12 flex items-center gap-2.5 px-4 border-b border-slate-800/80">
        <div className="w-7 h-7 rounded bg-blue-600 flex items-center justify-center text-white shrink-0 shadow-xs">
          <Gamepad2 className="w-4 h-4" />
        </div>
        <div className="flex flex-col min-w-0">
          <span className="text-sm font-semibold text-white tracking-tight truncate">
            L4D1 匹配管理
          </span>
          <span className="text-[10px] text-slate-400 font-mono leading-none">
            控制台工作台
          </span>
        </div>
      </div>

      {/* Navigation Menu */}
      <nav className="flex-1 px-2.5 py-3 space-y-1">
        {navItems.map((item) => {
          const isActive = activeTab === item.key;
          return (
            <button
              key={item.key}
              type="button"
              onClick={() => onTabChange(item.key)}
              className={`w-full flex items-center justify-between px-3 py-2 text-xs font-medium rounded-md transition-colors ${
                isActive
                  ? 'bg-blue-600 text-white shadow-xs'
                  : 'text-slate-300 hover:text-white hover:bg-slate-800/70'
              }`}
            >
              <div className="flex items-center gap-2.5 min-w-0">
                {item.icon}
                <span className="truncate">{item.label}</span>
              </div>
              {item.badge !== undefined && item.badge > 0 && (
                <span
                  className={`text-[10px] font-semibold px-1.5 py-0.2 rounded-full min-w-4 text-center ${
                    isActive ? 'bg-white text-blue-700' : item.badgeColor
                  }`}
                  title={`${item.badge} 项需关注`}
                >
                  {item.badge}
                </span>
              )}
            </button>
          );
        })}
      </nav>

      {/* Bottom Info */}
      <div className="p-3 border-t border-slate-800/80 bg-slate-900/40 text-[11px] text-slate-400 space-y-1">
        <div className="flex items-center justify-between">
          <span>调度周期</span>
          <span className="font-mono text-slate-300">5 秒轮询</span>
        </div>
        <div className="flex items-center justify-between">
          <span>协议适配</span>
          <span className="font-mono text-slate-300">A2S / Steam</span>
        </div>
      </div>
    </aside>
  );
};
