import { useState } from 'react';
import { X } from 'lucide-react';
import Stepper, { Step } from '../../components/react-bits/Stepper/Stepper';
import type { TargetServer, TargetServerInput, TargetServerObservation, WarmupStatus } from '../../api/models';
import { TargetServerForm } from './TargetServerForm';
import { formatObservationAge } from './server-view-model';

interface TargetServerDrawerProps {
  observation: TargetServerObservation | undefined;
  onClose: () => void;
  onDelete?: () => Promise<void>;
  onRefresh?: () => void | Promise<void>;
  onUpdate?: (input: TargetServerInput) => Promise<void>;
  server: TargetServer;
  warmups: WarmupStatus[];
}

type PendingAction =
  | { kind: 'disable'; input: TargetServerInput }
  | { kind: 'delete' }
  | null;

export function TargetServerDrawer({ observation, onClose, onDelete, onRefresh, onUpdate, server, warmups }: TargetServerDrawerProps) {
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const currentObservation = observation ?? { targetServerId: server.id, status: 'pending' as const, serverName: null, playerCount: null, maxPlayers: null, observedAt: null };
  const isOnline = currentObservation.status === 'online' && currentObservation.playerCount !== null && currentObservation.maxPlayers !== null;
  const title = isOnline && currentObservation.serverName ? currentObservation.serverName : server.endpoint;
  const relatedWarmups = warmups.filter(warmup => warmup.targetServerId === server.id);

  const runUpdate = async (input: TargetServerInput) => {
    if (!onUpdate) return;
    setBusy(true);
    setError(null);
    try {
      await onUpdate(input);
      await onRefresh?.();
      setEditing(false);
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : 'target_server_update_failed';
      if (message === 'target_server_drain_failed') {
        await onRefresh?.();
      }
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  const runDelete = async () => {
    if (!onDelete) return;
    setBusy(true);
    setError(null);
    try {
      await onDelete();
      await onRefresh?.();
      onClose();
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : 'target_server_delete_failed';
      if (message === 'target_server_drain_failed') {
        await onRefresh?.();
      }
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  const requestUpdate = (input: TargetServerInput) => {
    if (server.enabled && !input.enabled) {
      setPendingAction({ kind: 'disable', input });
      return;
    }
    void runUpdate(input);
  };

  const confirmPendingAction = async () => {
    const action = pendingAction;
    if (!action) return;
    setPendingAction(null);
    if (action.kind === 'disable') {
      await runUpdate(action.input);
      return;
    }
    await runDelete();
  };

  return <div className="target-server-drawer__backdrop" role="presentation"><aside aria-label={`${title} 配置`} aria-modal="true" className="target-server-drawer" role="dialog">
    <header className="target-server-drawer__header"><div><span>TARGET SERVER</span><h2>{title}</h2></div><button aria-label="关闭配置抽屉" className="icon-button" onClick={onClose} title="关闭配置抽屉" type="button"><X aria-hidden="true" size={17} /></button></header>
    <section className="target-server-drawer__live"><span>LIVE A2S SAMPLE</span><strong>{isOnline ? `${currentObservation.playerCount} / ${currentObservation.maxPlayers}` : '-- / --'}</strong><code>{server.endpoint}</code><small>{currentObservation.status === 'online' ? `A2S 在线 · ${formatObservationAge(currentObservation.observedAt)}` : currentObservation.status === 'unavailable' ? 'A2S 不可用' : '等待首次观测'}</small></section>
    <section className="target-server-drawer__configuration"><div className="target-server-drawer__section-title"><span>调度配置</span>{onUpdate && !editing ? <button onClick={() => { setEditing(true); setError(null); }} type="button">编辑配置</button> : null}</div><dl><div><dt>状态</dt><dd>{server.enabled ? '已启用' : '已禁用'}</dd></div><div><dt>模式</dt><dd>{server.requiresReservation ? '预留大厅' : '公共匹配'}</dd></div><div><dt>优先级</dt><dd>{server.priority}</dd></div><div><dt>并发上限</dt><dd>{server.maxConcurrentWarmups}</dd></div><div><dt>尝试窗口</dt><dd>{server.attemptWindowSeconds} 秒</dd></div><div><dt>人数目标</dt><dd>{server.playerTarget}</dd></div></dl>{editing ? <TargetServerForm busy={busy} error={error} onCancel={() => { setEditing(false); setError(null); }} onSubmit={requestUpdate} server={server} /> : null}</section>
    <section className="target-server-drawer__warmups"><span>关联暖服 {relatedWarmups.length}</span>{relatedWarmups.length === 0 ? <small>当前无关联暖服。</small> : relatedWarmups.map(warmup => <div key={warmup.operationId}><strong>{warmup.warmupAgentName}</strong><small>{warmup.state} · {warmup.phase}</small></div>)}</section>
    {pendingAction ? <ConfirmationFlow action={pendingAction} busy={busy} onCancel={() => setPendingAction(null)} onConfirm={() => void confirmPendingAction()} /> : null}
    {!editing && !pendingAction && onDelete ? <button className="target-server-drawer__delete" onClick={() => setPendingAction({ kind: 'delete' })} type="button">删除服务器</button> : null}
    {!editing && !pendingAction && onUpdate && server.enabled ? <button className="target-server-drawer__disable" onClick={() => setEditing(true)} type="button">禁用并编辑配置</button> : null}
    {error && !editing ? <p className="target-server-form__error">{error}</p> : null}
  </aside></div>;
}

function ConfirmationFlow({ action, busy, onCancel, onConfirm }: { action: Exclude<PendingAction, null>; busy: boolean; onCancel: () => void; onConfirm: () => void }) {
  const isDelete = action.kind === 'delete';
  const subject = isDelete ? '删除服务器' : '禁用服务器';
  return <section className="target-server-confirmation" aria-label={`${subject}确认`}><Stepper backButtonText="返回" completeButtonText={isDelete ? '确认删除' : '确认禁用'} nextButtonProps={{ disabled: busy }} nextButtonText="继续" onFinalStepCompleted={onConfirm}><Step><p>{isDelete ? 'Core 将先停止该服务器的 active/uncertain 暖服，然后删除配置。' : 'Core 将先停止该服务器的 active/uncertain 暖服，然后保存禁用状态。'}</p></Step><Step><p>此操作不会在浏览器端乐观移除服务器。确认后将读取 Core 最新状态。</p></Step></Stepper><button className="target-server-confirmation__cancel" onClick={onCancel} type="button">取消操作</button></section>;
}
