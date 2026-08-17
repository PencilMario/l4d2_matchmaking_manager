import Cubes from '../../components/react-bits/Cubes/Cubes';
import SpotlightCard from '../../components/react-bits/SpotlightCard/SpotlightCard';
import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';

interface WarmupWorkspaceProps {
  observations: TargetServerObservation[];
  onSelectTarget?: (targetServerId: string) => void;
  servers: TargetServer[];
  warmups: WarmupStatus[];
}

export function WarmupWorkspace({ observations, onSelectTarget, servers, warmups }: WarmupWorkspaceProps) {
  const uncertainCount = warmups.filter(warmup => warmup.state === 'uncertain').length;
  const unavailableCount = observations.filter(observation => observation.status === 'unavailable').length;

  return (
    <div className="warmup-workspace">
      <div className="warmup-workspace__summary" aria-label="暖服状态摘要">
        <SummaryCard label="活动暖服" value={warmups.length} tone="active" />
        <SummaryCard label="操作未确认" value={uncertainCount} tone="attention" />
        <SummaryCard label="A2S 不可用" value={unavailableCount} tone="failure" />
      </div>

      <section className="warmup-topology" aria-label="Target Server 拓扑筛选">
        <div aria-hidden="true" className="warmup-topology__cubes"><Cubes autoAnimate={false} borderStyle="1px solid rgba(88, 209, 215, .35)" faceColor="#12231f" gridSize={4} maxAngle={18} rippleColor="#b4dd59" /></div>
        <div className="warmup-topology__heading"><span>TOPOLOGY BAND</span><small>选择服务器以筛选资源表</small></div>
        <div className="warmup-topology__units">
          {servers.map(server => {
            const observation = observations.find(item => item.targetServerId === server.id);
            const status = observation?.status ?? 'pending';
            return <button className={`warmup-topology__unit is-${status}`} key={server.id} onClick={() => onSelectTarget?.(server.id)} type="button" aria-label={`筛选 ${server.endpoint}`}>
              <span>{observation?.serverName ?? server.endpoint}</span>
              <small>{status === 'online' ? `${observation?.playerCount ?? '--'} / ${observation?.maxPlayers ?? '--'}` : status === 'unavailable' ? 'A2S 不可用' : '等待首次观测'}</small>
            </button>;
          })}
        </div>
      </section>

      <section aria-label="暖服队列" className="warmup-queue">
        <div className="warmup-queue__header"><span>WARM-UP ATTEMPTS</span><small>{warmups.length} 条运行记录</small></div>
        {warmups.length === 0 ? <p className="workspace-empty">当前没有暖服尝试。</p> : <div className="warmup-queue__rows">
          {warmups.map(warmup => <article className="warmup-queue__row" key={warmup.operationId}>
            <div><strong>{warmup.targetEndpoint}</strong><code>{warmup.warmupAgentName} · {warmup.mode}</code></div>
            <div><span className={warmup.state === 'uncertain' ? 'is-uncertain' : 'is-active'}>{warmup.state === 'uncertain' ? '操作未确认' : warmup.state}</span><small>{warmup.phase}</small></div>
            <div><span>LOBBY</span><small>{warmup.lobbyId ?? '尚未返回'}</small></div>
            <div><span>剩余</span><strong>{warmup.remainingSeconds} 秒</strong></div>
          </article>)}
        </div>}
      </section>
    </div>
  );
}

function SummaryCard({ label, tone, value }: { label: string; tone: 'active' | 'attention' | 'failure'; value: number }) {
  return <SpotlightCard className={`warmup-summary warmup-summary--${tone}`} spotlightColor="rgba(88, 209, 215, .08)">
    <span>{label}</span><strong>{value}</strong>
  </SpotlightCard>;
}
