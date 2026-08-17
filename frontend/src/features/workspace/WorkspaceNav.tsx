import { Bot, Flame, Search, Server } from 'lucide-react';

export type WorkspaceView = 'warmups' | 'servers' | 'agents' | 'lobbies';
const entries = [{ id: 'warmups', label: '当前暖服', icon: Flame }, { id: 'servers', label: 'Target Server', icon: Server }, { id: 'agents', label: 'Warm-up Agent', icon: Bot }, { id: 'lobbies', label: 'Lobby 查询', icon: Search }] as const;

export function WorkspaceNav({ activeView, onChange }: { activeView: WorkspaceView; onChange: (view: WorkspaceView) => void }) {
  return <nav className="workspace-nav" aria-label="资源导航"><div className="workspace-nav__brand">L4D2</div><div className="workspace-nav__items">{entries.map(({ id, label, icon: Icon }) => <button className={activeView === id ? 'workspace-nav__item is-active' : 'workspace-nav__item'} key={id} onClick={() => onChange(id)} type="button"><Icon aria-hidden="true" size={17} /><span>{label}</span></button>)}</div><span className="workspace-nav__signal" aria-hidden="true" /></nav>;
}
