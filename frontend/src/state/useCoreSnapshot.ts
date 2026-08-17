import { useCallback, useEffect, useRef, useState } from 'react';
import type { LobbySnapshot, TargetServer, TargetServerInput, TargetServerObservation, WarmupAgent, WarmupAgentInput, WarmupStatus } from '../api/models';
import { isUnauthorized } from './core-errors';

export interface CoreSnapshotClient {
  listServers(signal?: AbortSignal): Promise<TargetServer[]>;
  listAgents(signal?: AbortSignal): Promise<WarmupAgent[]>;
  listWarmups(signal?: AbortSignal): Promise<WarmupStatus[]>;
  listServerObservations(signal?: AbortSignal): Promise<TargetServerObservation[]>;
  updateServer?(serverId: string, input: TargetServerInput): Promise<TargetServer>;
  deleteServer?(serverId: string): Promise<void>;
  createAgent?(input: WarmupAgentInput): Promise<WarmupAgent>;
  updateAgent?(agentId: string, input: WarmupAgentInput): Promise<WarmupAgent>;
  startAgent?(agentId: string): Promise<WarmupAgent>;
  stopAgent?(agentId: string): Promise<WarmupAgent>;
  recreateAgent?(agentId: string): Promise<WarmupAgent>;
  deleteAgent?(agentId: string): Promise<void>;
  queryLobby?(lobbyId: string): Promise<LobbySnapshot>;
}

export interface CoreSnapshot {
  servers: TargetServer[];
  agents: WarmupAgent[];
  warmups: WarmupStatus[];
  observations: TargetServerObservation[];
  loading: boolean;
  stale: boolean;
  error: string | null;
  refreshedAt: Date | null;
}

interface UseCoreSnapshotOptions {
  paused?: boolean;
  onUnauthorized?: () => void;
}

const initialSnapshot: CoreSnapshot = {
  servers: [],
  agents: [],
  warmups: [],
  observations: [],
  loading: true,
  stale: false,
  error: null,
  refreshedAt: null,
};

export function useCoreSnapshot(
  client: CoreSnapshotClient,
  { paused = false, onUnauthorized }: UseCoreSnapshotOptions = {},
) {
  const [snapshot, setSnapshot] = useState<CoreSnapshot>(initialSnapshot);
  const hasSuccessfulSnapshot = useRef(false);

  const refresh = useCallback(async (signal?: AbortSignal) => {
    try {
      const [servers, agents, warmups, observations] = await Promise.all([
        client.listServers(signal),
        client.listAgents(signal),
        client.listWarmups(signal),
        client.listServerObservations(signal),
      ]);
      hasSuccessfulSnapshot.current = true;
      setSnapshot({
        servers,
        agents,
        warmups,
        observations,
        loading: false,
        stale: false,
        error: null,
        refreshedAt: new Date(),
      });
    } catch (error) {
      if (signal?.aborted) {
        return;
      }
      if (isUnauthorized(error)) {
        onUnauthorized?.();
      }
      setSnapshot(current => ({
        ...current,
        loading: false,
        stale: hasSuccessfulSnapshot.current,
        error: error instanceof Error ? error.message : 'core_request_failed',
      }));
    }
  }, [client, onUnauthorized]);

  useEffect(() => {
    if (paused) {
      return;
    }
    const controller = new AbortController();
    void refresh(controller.signal);
    const interval = window.setInterval(() => void refresh(), 5_000);
    return () => {
      controller.abort();
      window.clearInterval(interval);
    };
  }, [paused, refresh]);

  return { ...snapshot, refresh };
}
