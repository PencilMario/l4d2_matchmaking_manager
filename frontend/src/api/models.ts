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
  keepVncAlive?: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface WarmupAgentInput {
  name: string;
  downloadRegion: string | null;
  keepVncAlive: boolean;
}

export interface GlobalSettings {
  steamProxyUrl: string | null;
  steamWebApiKeyConfigured: boolean;
  warmupSchedulingEnabled: boolean;
  updatedAt: string;
}

export interface GlobalSettingsInput {
  steamProxyUrl: string | null;
  steamWebApiKey?: string;
  clearSteamWebApiKey?: boolean;
}

export interface VncProxySettings {
  proxyUrl: string | null;
  updatedAt: string;
}

export interface VncProxySettingsInput {
  proxyUrl: string | null;
}

export interface SteamWebApiKeySettings {
  configured: boolean;
  updatedAt: string;
}

export interface SteamWebApiKeySettingsInput {
  apiKey?: string;
  clear?: boolean;
}

export interface WarmupSchedulingSettings {
  enabled: boolean;
  updatedAt: string;
}

export interface WarmupSchedulingSettingsInput {
  enabled: boolean;
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
  avatarUrl?: string | null;
}

export interface LobbySnapshot {
  lobbyId: string;
  ownerSteamId: string | null;
  members: LobbyMember[];
  metadata: Record<string, string>;
  observedAt: string;
  memberDataStatus: string;
}
