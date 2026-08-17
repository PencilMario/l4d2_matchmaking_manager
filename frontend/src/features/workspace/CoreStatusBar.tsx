import { RefreshCw } from 'lucide-react';

export function CoreStatusBar({ busy, stale, refreshedAt, onRefresh }: { busy: boolean; stale: boolean; refreshedAt: Date | null; onRefresh: () => void }) {
  const timestamp = refreshedAt ? refreshedAt.toLocaleTimeString('zh-CN', { hour12: false }) : '同步中';
  return <header className="core-status-bar"><div className="core-status-bar__state"><span aria-hidden="true" />{stale ? 'Core stale' : 'Core online'}</div><div className="core-status-bar__meta">LAST SYNC {timestamp}</div><button aria-label="刷新资源" className="icon-button" disabled={busy} onClick={onRefresh} title="刷新资源" type="button"><RefreshCw aria-hidden="true" size={16} className={busy ? 'is-spinning' : ''} /></button></header>;
}
