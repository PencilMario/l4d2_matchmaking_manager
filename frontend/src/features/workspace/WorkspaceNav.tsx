import { Bot, Gamepad2, LayoutDashboard, Search, Server, Settings } from 'lucide-react';

export type WorkspaceView = 'overview' | 'servers' | 'agents' | 'lobbies' | 'settings';
const entries = [{ id: 'overview', label: '系统概览', icon: LayoutDashboard }, { id: 'servers', label: '目标服务器', icon: Server }, { id: 'agents', label: '暖服节点', icon: Bot }, { id: 'lobbies', label: '大厅查询', icon: Search }, { id: 'settings', label: '全局设置', icon: Settings }] as const;

export function WorkspaceNav({ activeView, onChange, badges = {} }: { activeView: WorkspaceView; onChange: (view: WorkspaceView) => void; badges?: Partial<Record<WorkspaceView, number>> }) {
  return <aside className="workspace-nav"><div className="workspace-nav__brand"><span className="workspace-nav__logo"><Gamepad2 size={16} /></span><div><strong>L4D2 匹配管理</strong><small>控制台工作台</small></div></div><nav className="workspace-nav__items" aria-label="主导航">{entries.map(({ id, label, icon: Icon }) => <button aria-current={activeView === id ? 'page' : undefined} className={activeView === id ? 'workspace-nav__item is-active' : 'workspace-nav__item'} key={id} onClick={() => onChange(id)} type="button"><span className="workspace-nav__item-label"><Icon aria-hidden="true" size={16} /><span>{label}</span></span>{badges[id] ? <b className="workspace-nav__badge" title={`${badges[id]} 项需关注`}>{badges[id]}</b> : null}</button>)}</nav><div className="workspace-nav__footer"><span>调度周期<strong>5 秒轮询</strong></span><span>协议适配<strong>A2S / Steam</strong></span></div></aside>;
}
