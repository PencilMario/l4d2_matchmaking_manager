import { useMemo, useState } from 'react';
import type { CoreSnapshotClient } from '../../state/useCoreSnapshot';
import { useCoreSnapshot } from '../../state/useCoreSnapshot';
import { TargetServerDrawer } from '../servers/TargetServerDrawer';
import { TargetServerWorkspace } from '../servers/TargetServerWorkspace';
import { WarmupWorkspace } from '../warmups/WarmupWorkspace';
import { AgentWorkspace } from '../agents/AgentWorkspace';
import type { AgentAction } from '../agents/AgentTable';
import { LobbyQueryView } from '../lobbies/LobbyQueryView';
import { CoreStatusBar } from './CoreStatusBar';
import { WorkspaceNav, type WorkspaceView } from './WorkspaceNav';
import { SettingsWorkspace } from '../settings/SettingsWorkspace';

const viewTitle: Record<WorkspaceView, string> = { overview: '系统概览', servers: '目标服务器', agents: '暖服节点', lobbies: '大厅查询', settings: '全局设置' };

export function WorkspaceShell({ client, onUnauthorized, onSignOut = () => undefined }: { client: CoreSnapshotClient; onUnauthorized?: () => void; onSignOut?: () => void }) {
  const [activeView, setActiveView] = useState<WorkspaceView>('overview');
  const [openServerId, setOpenServerId] = useState<string | null>(null);
  const [selectedTargetId, setSelectedTargetId] = useState<string | null>(null);
  const snapshot = useCoreSnapshot(client, { onUnauthorized });
  const visibleServers = selectedTargetId ? snapshot.servers.filter(server => server.id === selectedTargetId) : snapshot.servers;
  const openServer = openServerId ? snapshot.servers.find(server => server.id === openServerId) : undefined;
  const agentActions = client.startAgent && client.stopAgent && client.recreateAgent && client.deleteAgent
    ? async (action: AgentAction, agent: typeof snapshot.agents[number]) => {
      if (action === 'start') await client.startAgent!(agent.id);
      if (action === 'stop') await client.stopAgent!(agent.id);
      if (action === 'recreate') await client.recreateAgent!(agent.id);
      if (action === 'delete') await client.deleteAgent!(agent.id);
    }
    : undefined;

  const selectTarget = (targetServerId: string) => {
    setSelectedTargetId(targetServerId);
    setActiveView('servers');
  };

  const summary = useMemo(() => ({
    active: snapshot.warmups.filter(item => item.state === 'active').length,
    uncertain: snapshot.warmups.filter(item => item.state === 'uncertain').length,
    unavailable: snapshot.observations.filter(item => item.status === 'unavailable').length,
    online: snapshot.observations.filter(item => item.status === 'online').length,
    running: snapshot.agents.filter(item => item.status === 'running').length,
    quarantined: snapshot.agents.filter(item => item.status === 'quarantined').length,
  }), [snapshot]);
  if (snapshot.loading && !snapshot.refreshedAt) return <main className="first-load" role="status"><span className="loader" /><p>正在读取控制服务数据</p></main>;
  return <main className="workspace-shell"><WorkspaceNav activeView={activeView} onChange={setActiveView} badges={{ overview: summary.uncertain, servers: summary.unavailable, agents: summary.quarantined }} /><section className="workspace-shell__main"><CoreStatusBar busy={snapshot.loading} onRefresh={() => void snapshot.refresh()} onSignOut={onSignOut} refreshedAt={snapshot.refreshedAt} stale={snapshot.stale} /><div className="workspace-shell__content"><div className="page-heading"><h1>{viewTitle[activeView]}</h1>{activeView === 'servers' && selectedTargetId ? <button className="button button--quiet" onClick={() => setSelectedTargetId(null)} type="button">清除筛选</button> : null}</div>{activeView === 'overview' ? <><section className="summary-strip" aria-label="系统摘要"><Summary label="正在进行的暖服任务" value={summary.active} /><Summary label="结果未确认的任务" tone="warning" value={summary.uncertain} /><Summary label="A2S 不可用服务器" tone="danger" value={summary.unavailable} /><Summary label="在线目标服务器" tone="success" value={summary.online} /><Summary label="运行中的暖服节点" tone="success" value={summary.running} /><Summary label="已隔离节点" tone="danger" value={summary.quarantined} /></section><WarmupWorkspace observations={snapshot.observations} onSelectTarget={selectTarget} servers={snapshot.servers} warmups={snapshot.warmups} /></> : null}{activeView === 'servers' ? <TargetServerWorkspace observations={snapshot.observations.filter(item => !selectedTargetId || item.targetServerId === selectedTargetId)} onCreate={client.createServer ? async input => { await client.createServer!(input); } : undefined} onOpenServer={server => setOpenServerId(server.id)} onRefresh={() => snapshot.refresh()} servers={visibleServers} warmups={snapshot.warmups} /> : null}{activeView === 'agents' ? <AgentWorkspace agents={snapshot.agents} onAction={agentActions} onCreate={client.createAgent ? async input => { await client.createAgent!(input); } : undefined} onRefresh={() => snapshot.refresh()} onUpdate={client.updateAgent ? async (agent, input) => { await client.updateAgent!(agent.id, input); } : undefined} warmups={snapshot.warmups} /> : null}{activeView === 'lobbies' ? client.queryLobby ? <LobbyQueryView queryLobby={lobbyId => client.queryLobby!(lobbyId)} /> : <p className="empty-state">大厅查询客户端不可用。</p> : null}{activeView === 'settings' ? client.getSettings && client.updateVncProxy && client.updateSteamWebApiKey ? <SettingsWorkspace getSettings={() => client.getSettings!()} updateProxy={input => client.updateVncProxy!(input)} updateKey={input => client.updateSteamWebApiKey!(input)} /> : <p className="empty-state">全局设置客户端不可用。</p> : null}{snapshot.error ? <p className="inline-error">控制服务请求失败：{snapshot.error}</p> : null}</div></section>{openServer ? <TargetServerDrawer observation={snapshot.observations.find(item => item.targetServerId === openServer.id)} onClose={() => setOpenServerId(null)} onDelete={client.deleteServer ? async () => { await client.deleteServer!(openServer.id); } : undefined} onRefresh={() => snapshot.refresh()} onUpdate={client.updateServer ? async input => { await client.updateServer!(openServer.id, input); } : undefined} server={openServer} warmups={snapshot.warmups} /> : null}</main>;
}

function Summary({ label, value, tone = 'normal' }: { label: string; value: number; tone?: 'normal' | 'success' | 'warning' | 'danger' }) { return <div className={`summary-value summary-value--${tone}`}><span>{label}</span><strong>{value}</strong></div>; }
