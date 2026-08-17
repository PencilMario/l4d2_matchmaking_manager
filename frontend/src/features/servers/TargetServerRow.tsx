import { Settings2 } from 'lucide-react';
import Counter from '../../components/react-bits/Counter/Counter';
import type { TargetServerViewModel } from './server-view-model';
import { formatObservationAge } from './server-view-model';

interface TargetServerRowProps {
  row: TargetServerViewModel;
  onOpen: (serverId: string) => void;
}

const statusCopy = {
  online: 'A2S 在线',
  unavailable: 'A2S 不可用',
  pending: '等待首次观测',
} as const;

export function TargetServerRow({ row, onOpen }: TargetServerRowProps) {
  const { activeWarmupCount, observation, server } = row;
  const livePopulation = observation.status === 'online' && observation.playerCount !== null && observation.maxPlayers !== null
    ? { current: observation.playerCount, maximum: observation.maxPlayers }
    : null;
  const isOnline = livePopulation !== null;
  const primaryName = isOnline && observation.serverName ? observation.serverName : server.endpoint;

  return (
    <article className="target-server-row" data-server-row={server.id}>
      <div className="target-server-row__identity">
        <strong>{primaryName}</strong>
        <code>{isOnline ? server.endpoint : statusCopy[observation.status]}</code>
      </div>
      <div className="target-server-row__population" aria-label={livePopulation ? `${livePopulation.current} / ${livePopulation.maximum}` : '-- / --'}>
        {livePopulation ? (
          <>
            <span className="sr-only">{livePopulation.current} / {livePopulation.maximum}</span>
            <Counter
              fontSize={18}
              fontWeight={700}
              gap={1}
              gradientHeight={0}
              horizontalPadding={0}
              textColor="var(--paper)"
              value={livePopulation.current}
            />
            <span className="target-server-row__slash">/</span>
            <Counter
              fontSize={18}
              fontWeight={700}
              gap={1}
              gradientHeight={0}
              horizontalPadding={0}
              textColor="var(--muted)"
              value={livePopulation.maximum}
            />
          </>
        ) : <strong>-- / --</strong>}
      </div>
      <div className={`target-server-row__observation is-${observation.status}`}>
        <span>{statusCopy[observation.status]}</span>
        <small>{formatObservationAge(observation.observedAt)}</small>
      </div>
      <div className="target-server-row__schedule">
        <span className={server.enabled ? 'is-enabled' : 'is-disabled'}>{server.enabled ? '已启用' : '已禁用'}</span>
        <small>优先级 {server.priority} · 并发 {server.maxConcurrentWarmups} · 暖服 {activeWarmupCount}</small>
      </div>
      <button aria-label={`配置 ${server.endpoint}`} className="icon-button target-server-row__action" onClick={() => onOpen(server.id)} title={`配置 ${server.endpoint}`} type="button">
        <Settings2 aria-hidden="true" size={16} />
      </button>
    </article>
  );
}
