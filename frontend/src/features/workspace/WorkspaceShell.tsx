import { useState } from 'react';
import type { CoreSnapshotClient } from '../../state/useCoreSnapshot';
import { useCoreSnapshot } from '../../state/useCoreSnapshot';
import { TargetServerDrawer } from '../servers/TargetServerDrawer';
import { TargetServerWorkspace } from '../servers/TargetServerWorkspace';
import { WarmupWorkspace } from '../warmups/WarmupWorkspace';
import { AgentWorkspace } from '../agents/AgentWorkspace';
import type { AgentAction } from '../agents/AgentTable';
import { LobbyQueryView } from '../lobbies/LobbyQueryView';
import { CoreStatusBar } from './CoreStatusBar';
import { VisualStage } from './VisualStage';
import { WorkspaceNav, type WorkspaceView } from './WorkspaceNav';

const viewTitle: Record<WorkspaceView, string> = { warmups: '当前暖服', servers: 'Target Server', agents: 'Warm-up Agent', lobbies: 'Lobby 查询' };

export function WorkspaceShell({ client, onUnauthorized }: { client: CoreSnapshotClient; onUnauthorized?: () => void }) {
  const [activeView, setActiveView] = useState<WorkspaceView>('warmups');
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

  return <main className="workspace-shell"><VisualStage /><div className="workspace-shell__data" data-workspace-data><WorkspaceNav activeView={activeView} onChange={setActiveView} /><section className="workspace-shell__main"><CoreStatusBar busy={snapshot.loading} onRefresh={() => void snapshot.refresh()} refreshedAt={snapshot.refreshedAt} stale={snapshot.stale} /><div className="workspace-shell__content"><p className="workspace-shell__eyebrow">RESOURCE WORKSPACE</p><div className="workspace-shell__title"><h1>{viewTitle[activeView]}</h1>{activeView === 'servers' && selectedTargetId ? <button className="workspace-filter-clear" onClick={() => setSelectedTargetId(null)} type="button">清除拓扑筛选</button> : null}</div>{activeView === 'warmups' ? <WarmupWorkspace observations={snapshot.observations} onSelectTarget={selectTarget} servers={snapshot.servers} warmups={snapshot.warmups} /> : null}{activeView === 'servers' ? <TargetServerWorkspace observations={snapshot.observations.filter(item => !selectedTargetId || item.targetServerId === selectedTargetId)} onCreate={client.createServer ? async input => { await client.createServer!(input); } : undefined} onOpenServer={server => setOpenServerId(server.id)} onRefresh={() => snapshot.refresh()} servers={visibleServers} warmups={snapshot.warmups} /> : null}{activeView === 'agents' ? <AgentWorkspace agents={snapshot.agents} onAction={agentActions} onCreate={client.createAgent ? async input => { await client.createAgent!(input); } : undefined} onRefresh={() => snapshot.refresh()} onUpdate={client.updateAgent ? async (agent, input) => { await client.updateAgent!(agent.id, input); } : undefined} warmups={snapshot.warmups} /> : null}{activeView === 'lobbies' ? client.queryLobby ? <LobbyQueryView queryLobby={lobbyId => client.queryLobby!(lobbyId)} /> : <p className="workspace-empty">Lobby 查询客户端不可用。</p> : null}{snapshot.error ? <p className="workspace-shell__error">{snapshot.error}</p> : null}</div></section></div>{openServer ? <TargetServerDrawer observation={snapshot.observations.find(item => item.targetServerId === openServer.id)} onClose={() => setOpenServerId(null)} onDelete={client.deleteServer ? async () => { await client.deleteServer!(openServer.id); } : undefined} onRefresh={() => snapshot.refresh()} onUpdate={client.updateServer ? async input => { await client.updateServer!(openServer.id, input); } : undefined} server={openServer} warmups={snapshot.warmups} /> : null}</main>;
}
