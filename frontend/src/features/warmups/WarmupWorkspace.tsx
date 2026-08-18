import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';
import { formatRemaining, formatTime, labelWarmupPhase, labelWarmupState } from '../../state/display';

interface WarmupWorkspaceProps {
  observations: TargetServerObservation[];
  onSelectTarget?: (targetServerId: string) => void;
  servers: TargetServer[];
  warmups: WarmupStatus[];
}

export function WarmupWorkspace({ observations, onSelectTarget, servers: _servers, warmups }: WarmupWorkspaceProps) {
  const uncertainCount = warmups.filter(warmup => warmup.state === 'uncertain').length;
  const unavailableCount = observations.filter(observation => observation.status === 'unavailable').length;

  return (
    <div className="warmup-workspace">
      <section aria-label="当前暖服任务" className="data-table"><div className="data-table__head warmup-grid"><span>目标服务器</span><span>暖服节点</span><span>运行模式</span><span>当前状态</span><span>当前阶段</span><span>大厅 ID</span><span>剩余时间</span><span>开始时间</span></div>{warmups.length === 0 ? <p className="empty-state">当前没有进行中的暖服任务。</p> : warmups.map(warmup => <div className="data-table__row warmup-grid" key={warmup.operationId}><button className="text-button truncate" onClick={() => onSelectTarget?.(warmup.targetServerId)} title={warmup.targetEndpoint} type="button">{warmup.targetEndpoint}</button><span className="truncate" title={warmup.warmupAgentName}>{warmup.warmupAgentName}</span><span>{warmup.mode}</span><span className={`status status--${warmup.state === 'uncertain' ? 'warning' : 'success'}`} title={warmup.state}>{labelWarmupState(warmup.state)}</span><span title={warmup.phase}>{labelWarmupPhase(warmup.phase)}</span><code className="truncate" title={warmup.lobbyId ?? ''}>{warmup.lobbyId ?? '尚未返回'}</code><span>{formatRemaining(warmup.remainingSeconds)}</span><span>{formatTime(warmup.startedAt)}</span></div>)}</section>
      {(uncertainCount > 0 || unavailableCount > 0) && <section className="issues"><h2>需要人工处理</h2>{uncertainCount > 0 && <p>有 {uncertainCount} 个暖服任务的结果未确认，请检查任务状态。</p>}{unavailableCount > 0 && <p>有 {unavailableCount} 台目标服务器的 A2S 不可用，请检查服务器地址和网络。</p>}</section>}
    </div>
  );
}
