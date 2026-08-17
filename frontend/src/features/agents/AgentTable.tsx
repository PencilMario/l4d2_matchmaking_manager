import { Pencil, Play, RotateCw, Square, Trash2 } from 'lucide-react';
import type { WarmupAgent, WarmupStatus } from '../../api/models';

export type AgentAction = 'start' | 'stop' | 'recreate' | 'delete';

interface AgentTableProps {
  agents: WarmupAgent[];
  onAction?: (action: AgentAction, agent: WarmupAgent) => void;
  onEdit?: (agent: WarmupAgent) => void;
  warmups: WarmupStatus[];
}

export function AgentTable({ agents, onAction, onEdit, warmups }: AgentTableProps) {
  if (agents.length === 0) return <p className="workspace-empty">尚未配置 Warm-up Agent。</p>;
  return <section aria-label="Warm-up Agent 资源表" className="agent-table"><div aria-hidden="true" className="agent-table__header"><span>Agent</span><span>状态</span><span>下载区域 / noVNC</span><span>暖服</span><span>操作</span></div>{agents.map(agent => <AgentRow agent={agent} key={agent.id} onAction={onAction} onEdit={onEdit} warmupCount={warmups.filter(warmup => warmup.warmupAgentId === agent.id).length} />)}</section>;
}

function AgentRow({ agent, onAction, onEdit, warmupCount }: { agent: WarmupAgent; onAction?: (action: AgentAction, agent: WarmupAgent) => void; onEdit?: (agent: WarmupAgent) => void; warmupCount: number }) {
  const unavailableForAutomation = agent.status === 'quarantined';
  return <article className={`agent-table__row is-${agent.status}`}><div><strong>{agent.name}</strong><code>{agent.id}</code></div><div><span>{agent.status}</span>{unavailableForAutomation ? <small>需人工调查</small> : null}</div><div><span>{agent.downloadRegion?.trim() || 'Steam 默认'}</span><small>仅本机 :{agent.noVncPort}</small></div><div><span>{warmupCount}</span><small>关联暖服</small></div><div className="agent-table__actions">{onEdit ? <button aria-label={`编辑 ${agent.name}`} className="agent-table__action" onClick={() => onEdit(agent)} title={`编辑 ${agent.name}`} type="button"><Pencil aria-hidden="true" size={14} /></button> : null}{agent.status === 'running' ? <ActionButton action="stop" agent={agent} icon={<Square aria-hidden="true" size={14} />} label="停止" onAction={onAction} /> : null}{(agent.status === 'stopped' || agent.status === 'created') ? <ActionButton action="start" agent={agent} icon={<Play aria-hidden="true" size={14} />} label="启动" onAction={onAction} /> : null}{!unavailableForAutomation ? <ActionButton action="recreate" agent={agent} icon={<RotateCw aria-hidden="true" size={14} />} label="重建" onAction={onAction} /> : null}<ActionButton action="delete" agent={agent} icon={<Trash2 aria-hidden="true" size={14} />} label="删除" onAction={onAction} /></div></article>;
}

function ActionButton({ action, agent, icon, label, onAction }: { action: AgentAction; agent: WarmupAgent; icon: React.ReactNode; label: string; onAction?: (action: AgentAction, agent: WarmupAgent) => void }) {
  return <button aria-label={`${label} ${agent.name}`} className={`agent-table__action is-${action}`} onClick={() => onAction?.(action, agent)} title={`${label} ${agent.name}`} type="button">{icon}</button>;
}
