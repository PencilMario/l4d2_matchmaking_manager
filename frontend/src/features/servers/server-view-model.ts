import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';

export interface TargetServerViewModel {
  activeWarmupCount: number;
  observation: TargetServerObservation;
  server: TargetServer;
}

const pendingObservation = (targetServerId: string): TargetServerObservation => ({
  targetServerId,
  status: 'pending',
  serverName: null,
  playerCount: null,
  maxPlayers: null,
  observedAt: null,
});

export function buildTargetServerRows(
  servers: TargetServer[],
  observations: TargetServerObservation[],
  warmups: WarmupStatus[],
): TargetServerViewModel[] {
  const observationsByServerId = new Map(observations.map(observation => [observation.targetServerId, observation]));
  const activeWarmupsByServerId = new Map<string, number>();
  for (const warmup of warmups) {
    activeWarmupsByServerId.set(warmup.targetServerId, (activeWarmupsByServerId.get(warmup.targetServerId) ?? 0) + 1);
  }

  return servers.map(server => ({
    activeWarmupCount: activeWarmupsByServerId.get(server.id) ?? 0,
    observation: observationsByServerId.get(server.id) ?? pendingObservation(server.id),
    server,
  }));
}

export function formatObservationAge(observedAt: string | null, now = Date.now()): string {
  if (!observedAt) {
    return '等待首次观测';
  }
  const seconds = Math.max(0, Math.floor((now - new Date(observedAt).getTime()) / 1_000));
  if (seconds < 60) {
    return `${seconds} 秒前`;
  }
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `${minutes} 分钟前`;
  }
  return `${Math.floor(minutes / 60)} 小时前`;
}
