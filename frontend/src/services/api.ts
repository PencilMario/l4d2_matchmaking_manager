import type { LobbyLookupResult } from '../types';

export const AUTH_TOKEN_KEY = 'l4d2_mgmt_access_token';
let token = localStorage.getItem(AUTH_TOKEN_KEY) || '';
let onUnauthorized: (() => void) | null = null;

export function setAuthToken(value: string) {
  token = value;
  value ? localStorage.setItem(AUTH_TOKEN_KEY, value) : localStorage.removeItem(AUTH_TOKEN_KEY);
}
export function getAuthToken() { return token; }
export function setUnauthorizedHandler(handler: () => void) { onUnauthorized = handler; }

type RawServer = { id: string; endpoint: string; requiresReservation: boolean; priority: number; maxConcurrentWarmups: number; attemptWindowSeconds: number; playerTarget: number; enabled: boolean; hasRconCredentials: boolean; createdAt: string; updatedAt: string };
type RawObservation = { targetServerId: string; status: string; serverName: string | null; playerCount: number | null; maxPlayers: number | null; observedAt: string | null };
type RawAgent = { id: string; name: string; status: string; ready?: boolean; downloadRegion: string | null; keepVncAlive: boolean; noVncPort: number; createdAt: string; updatedAt: string };
type AgentVncSession = { url: string; expiresAt: string };
type RawWarmup = { targetServerId: string; targetEndpoint: string; warmupAgentId: string; warmupAgentName: string; operationId: string; lobbyId: string | null; mode: string; state: string; phase: string; startedAt: string; observedAt: string; deadline: string; remainingSeconds: number };

async function request<T = any>(path: string, init: RequestInit = {}) {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (init.body) headers.set('Content-Type', 'application/json');
  const response = await fetch(path, { ...init, headers });
  if (response.status === 401) { onUnauthorized?.(); return { data: null as T | null, error: '访问令牌无效或已过期，请重新登录。', status: 401 }; }
  if (!response.ok) { const body = await response.text(); return { data: null as T | null, error: body || `http_${response.status}`, status: response.status }; }
  return { data: response.status === 204 ? null as T | null : await response.json() as T, error: null, status: response.status };
}

function serverInput(data: any) {
  return { endpoint: data.endpoint, requiresReservation: Boolean(data.requiresReservation), priority: data.priority ?? 0, maxConcurrentWarmups: data.maxConcurrentWarmups ?? 36, attemptWindowSeconds: data.attemptWindowSeconds ?? 720, playerTarget: data.playerTarget ?? 6, enabled: data.enabled ?? true, rconPassword: data.rconPassword || null };
}

export const ApiService = {
  async fetchAllState(): Promise<any> {
    const [servers, agents, warmups, observations, health] = await Promise.all([
      request<RawServer[]>('/v1/servers'), request<RawAgent[]>('/v1/agents'), request<RawWarmup[]>('/v1/warmups'), request<RawObservation[]>('/v1/servers/observations'), request<{ status: string }>('/healthz'),
    ]);
    const obs = new Map((observations.data || []).map(item => [item.targetServerId, item]));
    const serverWindows = new Map((servers.data || []).map(item => [item.id, item.attemptWindowSeconds]));
    const attempts = (warmups.data || []).map(item => ({ id: item.operationId, targetServerId: item.targetServerId, targetServerEndpoint: item.targetEndpoint, targetServerName: obs.get(item.targetServerId)?.serverName || undefined, agentId: item.warmupAgentId, agentName: item.warmupAgentName, operationMode: item.mode, status: item.state, phase: item.phase, lobbyId: item.lobbyId || undefined, remainingSeconds: item.remainingSeconds, totalSeconds: serverWindows.get(item.targetServerId) ?? 720, startedAt: item.startedAt, updatedAt: item.observedAt }));
    return {
      targets: (servers.data || []).map(item => { const observation = obs.get(item.id); return { ...item, name: observation?.serverName || undefined, currentPlayers: observation?.playerCount || 0, maxPlayers: observation?.maxPlayers || 0, a2sStatus: (observation?.status || 'pending') as any, lastObservedAt: observation?.observedAt || undefined, activeWarmupsCount: attempts.filter(attempt => attempt.targetServerId === item.id && ['active', 'uncertain'].includes(attempt.status)).length, hasRconPassword: item.hasRconCredentials }; }),
      agents: (agents.data || []).map(item => ({ ...item, ready: item.status === 'running' ? Boolean(item.ready) : undefined, keepVncAlive: Boolean(item.keepVncAlive), steamRegion: item.downloadRegion || undefined, novncPort: item.noVncPort, activeAttemptsCount: attempts.filter(attempt => attempt.agentId === item.id && ['active', 'uncertain'].includes(attempt.status)).length })),
      attempts,
      observations: (observations.data || []).map(item => ({ serverId: item.targetServerId, endpoint: (servers.data || []).find(server => server.id === item.targetServerId)?.endpoint || '', name: item.serverName || '', a2sStatus: item.status as any, players: item.playerCount || 0, maxPlayers: item.maxPlayers || 0, observedAt: item.observedAt || '' })),
      healthy: health.status === 200,
    };
  },
  createTarget(data: any) { return request('/v1/servers', { method: 'POST', body: JSON.stringify(serverInput(data)) }); },
  updateTarget(id: string, data: any) { return request(`/v1/servers/${id}`, { method: 'PUT', body: JSON.stringify(serverInput(data)) }); },
  deleteTarget(id: string) { return request<{ success?: boolean }>(`/v1/servers/${id}`, { method: 'DELETE' }); },
  toggleTargetEnabled(id: string, enabled: boolean) { return this.fetchAllState().then(state => this.updateTarget(id, { ...state.targets.find((item: any) => item.id === id), enabled })); },
  createAgent(data: any) { return request('/v1/agents', { method: 'POST', body: JSON.stringify({ name: data.name, downloadRegion: data.steamRegion || null, keepVncAlive: Boolean(data.keepVncAlive) }) }); },
  updateAgent(id: string, data: any) { return request(`/v1/agents/${id}`, { method: 'PUT', body: JSON.stringify({ name: data.name, downloadRegion: data.steamRegion || null, keepVncAlive: Boolean(data.keepVncAlive) }) }); },
  startAgent(id: string) { return request(`/v1/agents/${id}/start`, { method: 'POST' }); },
  stopAgent(id: string) { return request(`/v1/agents/${id}/stop`, { method: 'POST' }); },
  rebuildAgent(id: string) { return request(`/v1/agents/${id}/recreate`, { method: 'POST' }); },
  openAgentVncSession(id: string) { return request<AgentVncSession>(`/v1/agents/${id}/vnc-sessions`, { method: 'POST' }); },
  deleteAgent(id: string) { return request<{ success?: boolean }>(`/v1/agents/${id}`, { method: 'DELETE' }); },
  getGlobalSettings() { return request<{ steamProxyUrl: string | null; steamWebApiKeyConfigured: boolean; updatedAt: string }>('/v1/settings'); },
  updateGlobalSettings(input: { steamProxyUrl: string | null; steamWebApiKey?: string; clearSteamWebApiKey?: boolean }) { return request<{ steamProxyUrl: string | null; steamWebApiKeyConfigured: boolean; updatedAt: string }>('/v1/settings', { method: 'PUT', body: JSON.stringify(input) }); },
  async lookupLobby(id: string) {
    const result = await request<any>(`/v1/lobbies/${encodeURIComponent(id)}`);
    if (!result.data) return result;
    const raw = result.data;
    return { data: { lobbyId: raw.lobbyId, ownerSteamId: raw.ownerSteamId || '', memberStatus: raw.memberDataStatus === 'complete' ? 'complete' : 'unconfirmed', confirmedMemberCount: raw.memberDataStatus === 'complete' ? raw.members.length : null, observedAt: raw.observedAt, members: raw.members.map((member: any) => ({ ...member, isReady: false, joinedAt: raw.observedAt })), metadata: raw.metadata } as LobbyLookupResult, error: null, status: 200 };
  },
};
