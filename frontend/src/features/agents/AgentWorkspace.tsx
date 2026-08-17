import { useState } from 'react';
import Stepper, { Step } from '../../components/react-bits/Stepper/Stepper';
import type { WarmupAgent, WarmupAgentInput, WarmupStatus } from '../../api/models';
import { AgentForm } from './AgentForm';
import { AgentTable, type AgentAction } from './AgentTable';

interface AgentWorkspaceProps {
  agents: WarmupAgent[];
  onAction?: (action: AgentAction, agent: WarmupAgent) => Promise<void>;
  onCreate?: (input: WarmupAgentInput) => Promise<void>;
  onRefresh: () => void | Promise<void>;
  onUpdate?: (agent: WarmupAgent, input: WarmupAgentInput) => Promise<void>;
  warmups: WarmupStatus[];
}

type Confirmation = { action: 'recreate' | 'delete'; agent: WarmupAgent } | null;

export function AgentWorkspace({ agents, onAction, onCreate, onRefresh, onUpdate, warmups }: AgentWorkspaceProps) {
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<Confirmation>(null);
  const [editingAgent, setEditingAgent] = useState<WarmupAgent | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  const executeAction = async (action: AgentAction, agent: WarmupAgent) => {
    if (!onAction) return;
    setBusy(true); setError(null);
    try { await onAction(action, agent); } catch (caught) { setError(caught instanceof Error ? caught.message : 'warmup_agent_action_failed'); } finally { await onRefresh(); setBusy(false); }
  };
  const saveAgent = async (input: WarmupAgentInput) => {
    setBusy(true); setError(null);
    try {
      if (editingAgent) await onUpdate?.(editingAgent, input);
      else await onCreate?.(input);
      setEditingAgent(undefined);
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'warmup_agent_save_failed'); } finally { await onRefresh(); setBusy(false); }
  };
  const requestAction = (action: AgentAction, agent: WarmupAgent) => {
    if (!onAction) return;
    if (action === 'recreate' || action === 'delete') { setConfirmation({ action, agent }); return; }
    void executeAction(action, agent);
  };
  const confirmAction = async () => {
    if (!confirmation) return;
    const action = confirmation;
    setConfirmation(null);
    await executeAction(action.action, action.agent);
  };

  return <div className="agent-workspace"><div className="agent-workspace__heading"><span>WARM-UP AGENT FLEET</span>{onCreate ? <button onClick={() => { setEditingAgent(null); setError(null); }} type="button">创建 Agent</button> : null}</div>{editingAgent !== undefined ? <AgentForm agent={editingAgent ?? undefined} busy={busy} error={error} onCancel={() => setEditingAgent(undefined)} onSubmit={input => void saveAgent(input)} /> : null}<AgentTable agents={agents} onAction={onAction ? requestAction : undefined} onEdit={onUpdate ? agent => { setEditingAgent(agent); setError(null); } : undefined} warmups={warmups} />{confirmation ? <AgentConfirmation action={confirmation.action} busy={busy} onCancel={() => setConfirmation(null)} onConfirm={() => void confirmAction()} /> : null}{error && editingAgent === undefined ? <p className="target-server-form__error">{error}</p> : null}</div>;
}

function AgentConfirmation({ action, busy, onCancel, onConfirm }: { action: 'recreate' | 'delete'; busy: boolean; onCancel: () => void; onConfirm: () => void }) {
  const recreate = action === 'recreate';
  return <section aria-label={`${recreate ? '重建' : '删除'} Agent 确认`} className="agent-confirmation"><Stepper backButtonText="返回" completeButtonText={recreate ? '确认重建' : '确认删除'} nextButtonProps={{ disabled: busy }} nextButtonText="继续" onFinalStepCompleted={onConfirm}><Step><p>{recreate ? 'Core 会保留 Steam 登录与账号配置卷，并重建 Agent 容器。' : 'Core 会删除 Agent 容器与记录，但保留 Steam 登录和账号配置卷。'}</p></Step><Step><p>确认后将刷新完整 Agent 列表；操作失败时不会在浏览器端移除该行。</p></Step></Stepper><button onClick={onCancel} type="button">取消操作</button></section>;
}
