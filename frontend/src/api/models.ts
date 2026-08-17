export interface HealthSnapshot {
  status: string;
}

export interface TargetServer {
  id: string;
  endpoint: string;
  requiresReservation: boolean;
  priority: number;
  maxConcurrentWarmups: number;
  attemptWindowSeconds: number;
  playerTarget: number;
  enabled: boolean;
  hasRconCredentials: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface TargetServerInput {
  endpoint: string;
  requiresReservation: boolean;
  priority: number | null;
  maxConcurrentWarmups: number | null;
  attemptWindowSeconds: number | null;
  playerTarget: number | null;
  enabled: boolean | null;
  rconPassword: string | null;
}

export interface TargetServerObservation {
  targetServerId: string;
  status: 'online' | 'unavailable' | 'pending';
  serverName: string | null;
  playerCount: number | null;
  maxPlayers: number | null;
  observedAt: string | null;
}

export interface WarmupAgent {
  id: string;
  name: string;
  status: string;
  downloadRegion: string | null;
  noVncPort: number;
  createdAt: string;
  updatedAt: string;
}

export interface WarmupAgentInput {
  name: string;
  downloadRegion: string | null;
}

export interface WarmupStatus {
  targetServerId: string;
  targetEndpoint: string;
  warmupAgentId: string;
  warmupAgentName: string;
  operationId: string;
  lobbyId: string | null;
  mode: string;
  state: string;
  phase: string;
  startedAt: string;
  lobbyReadyAt: string | null;
  firstExternalMemberAt: string | null;
  quietSince: string | null;
  observedAt: string;
  deadline: string;
  remainingSeconds: number;
}

export interface LobbyMember {
  steamId: string;
  personaName: string | null;
}

export interface LobbySnapshot {
  lobbyId: string;
  ownerSteamId: string | null;
  members: LobbyMember[];
  metadata: Record<string, string>;
  observedAt: string;
  memberDataStatus: string;
}
