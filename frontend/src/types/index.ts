export type A2sStatus = 'online' | 'unavailable' | 'pending' | 'stale';

export type AgentStatus = 'running' | 'restarting' | 'stopped' | 'created' | 'quarantined';

export type AttemptStatus = 'active' | 'uncertain' | 'completed' | 'failed' | 'cancelled' | string;

export type AttemptPhase =
  | 'Selecting'
  | 'AwaitingFirstMember'
  | 'Active'
  | string;

export type OperationMode = 'standard' | 'direct_lobby' | 'reservation_hold' | string;

export type TargetServerGameMode = 'coop' | 'versus';

export interface SteamDownloadRegion {
  id: number;
  name: string;
}

export interface TargetServer {
  id: string;
  endpoint: string;
  name?: string;
  currentPlayers: number;
  maxPlayers: number;
  a2sStatus: A2sStatus;
  lastObservedAt?: string;
  enabled: boolean;
  priority: number;
  requiresReservation: boolean;
  maxConcurrentWarmups: number;
  activeWarmupsCount: number;
  attemptWindowSeconds: number;
  playerTarget: number;
  gameMode?: TargetServerGameMode | null;
  hasRconPassword?: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface WarmupAgent {
  id: string;
  name: string;
  status: AgentStatus;
  ready?: boolean;
  steamRegion?: string;
  keepVncAlive: boolean;
  novncPort: number;
  activeAttemptsCount: number;
  createdAt: string;
  updatedAt: string;
  quarantineReason?: string;
}

export interface WarmupAttempt {
  id: string;
  targetServerId: string;
  targetServerEndpoint: string;
  targetServerName?: string;
  agentId: string;
  agentName: string;
  operationMode: OperationMode;
  status: AttemptStatus;
  phase: AttemptPhase;
  lobbyId?: string;
  remainingSeconds: number;
  totalSeconds: number;
  startedAt: string;
  updatedAt: string;
  errorMessage?: string;
}

export interface ServerObservation {
  serverId: string;
  endpoint: string;
  name: string;
  a2sStatus: A2sStatus;
  players: number;
  maxPlayers: number;
  mapName?: string;
  pingMs?: number;
  observedAt: string;
}

export interface LobbyMember {
  steamId: string;
  personaName: string;
  avatarUrl?: string | null;
  isReady: boolean;
  joinedAt: string;
  ping?: number;
}

export interface LobbyLookupResult {
  lobbyId: string;
  ownerSteamId: string;
  memberStatus: 'complete' | 'unconfirmed' | 'empty';
  confirmedMemberCount: number | null;
  observedAt: string;
  members: LobbyMember[];
  metadata: Record<string, string>;
  isStale?: boolean;
}

export interface AppStateData {
  targets: TargetServer[];
  agents: WarmupAgent[];
  attempts: WarmupAttempt[];
  observations: ServerObservation[];
  lastUpdated: Date | null;
  controllerHealthy: boolean;
  initialLoaded: boolean;
  isRefreshing: boolean;
}

export type TabKey = 'overview' | 'targets' | 'agents' | 'lobby' | 'settings';
