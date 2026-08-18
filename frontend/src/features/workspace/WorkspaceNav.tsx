import { Bot, LayoutDashboard, Search, Server } from 'lucide-react';

export type WorkspaceView = 'overview' | 'servers' | 'agents' | 'lobbies';
const entries = [{ id: 'overview', label: '系统概览', icon: LayoutDashboard }, { id: 'servers', label: '目标服务器', icon: Server }, { id: 'agents', label: '暖服节点', icon: Bot }, { id: 'lobbies', label: '大厅查询', icon: Search }] as const;

export function WorkspaceNav({ activeView, onChange }: { activeView: WorkspaceView; onChange: (view: WorkspaceView) => void }) {
  return <nav className="workspace-nav" aria-label="主导航"><div className="workspace-nav__brand">L4D2 <span>匹配管理</span></div><div className="workspace-nav__items">{entries.map(({ id, label, icon: Icon }) => <button aria-current={activeView === id ? 'page' : undefined} className={activeView === id ? 'workspace-nav__item is-active' : 'workspace-nav__item'} key={id} onClick={() => onChange(id)} type="button"><Icon aria-hidden="true" size={18} /><span>{label}</span></button>)}</div></nav>;
}
