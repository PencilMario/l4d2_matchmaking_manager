import { LogOut, RefreshCw } from 'lucide-react';

export function CoreStatusBar({ busy, stale, refreshedAt, onRefresh, onSignOut }: { busy: boolean; stale: boolean; refreshedAt: Date | null; onRefresh: () => void; onSignOut: () => void }) {
  const timestamp = refreshedAt ? refreshedAt.toLocaleTimeString('zh-CN', { hour12: false }) : '正在读取';
  return <header className="core-status-bar"><div className={`core-status-bar__state ${stale ? 'is-error' : ''}`}><span aria-hidden="true" />{stale ? '控制服务连接异常' : '控制服务正常'}</div><div className="core-status-bar__meta">最后更新于 {timestamp}</div><div className="core-status-bar__actions"><button aria-label="手动刷新" className="icon-button" onClick={onRefresh} title="手动刷新" type="button"><RefreshCw size={16} className={busy ? 'spin' : ''} /></button><button aria-label="退出当前令牌会话" className="icon-button" onClick={onSignOut} title="退出当前令牌会话" type="button"><LogOut size={16} /></button></div></header>;
}
