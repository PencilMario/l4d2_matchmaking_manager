import type {
  HealthSnapshot,
  LobbySnapshot,
  TargetServer,
  TargetServerInput,
  TargetServerObservation,
  WarmupAgent,
  WarmupAgentInput,
  WarmupStatus,
  GlobalSettings,
  GlobalSettingsInput,
  SteamWebApiKeySettings,
  SteamWebApiKeySettingsInput,
  VncProxySettings,
  VncProxySettingsInput,
  WarmupSchedulingSettings,
  WarmupSchedulingSettingsInput,
  WarmupPauseWindowsSettings,
  WarmupPauseWindowsSettingsInput,
} from './models';

type Fetcher = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export class CoreApiError extends Error {
  readonly retryable: boolean;

  constructor(
    readonly status: number,
    readonly code: string,
  ) {
    super(code);
    this.name = 'CoreApiError';
    this.retryable = status === 408 || status === 429 || status >= 500;
  }
}

export class CoreClient {
  constructor(
    private readonly getToken: () => string | null,
    private readonly fetcher: Fetcher = (...args) => globalThis.fetch(...args),
  ) {}

  getHealth(signal?: AbortSignal) {
    return this.request<HealthSnapshot>('/healthz', { authenticated: false, signal });
  }

  listServers(signal?: AbortSignal) {
    return this.request<TargetServer[]>('/v1/servers', { signal });
  }

  listServerObservations(signal?: AbortSignal) {
    return this.request<TargetServerObservation[]>('/v1/servers/observations', { signal });
  }

  listAgents(signal?: AbortSignal) {
    return this.request<WarmupAgent[]>('/v1/agents', { signal });
  }

  listWarmups(signal?: AbortSignal) {
    return this.request<WarmupStatus[]>('/v1/warmups', { signal });
  }

  getSettings(signal?: AbortSignal) {
    return this.request<GlobalSettings>('/v1/settings', { signal });
  }

  updateSettings(input: GlobalSettingsInput) {
    return this.request<GlobalSettings>('/v1/settings', { method: 'PUT', body: input });
  }

  getVncProxy(signal?: AbortSignal) {
    return this.request<VncProxySettings>('/v1/settings/vnc-proxy', { signal });
  }

  updateVncProxy(input: VncProxySettingsInput) {
    return this.request<VncProxySettings>('/v1/settings/vnc-proxy', { method: 'PUT', body: input });
  }

  getSteamWebApiKey(signal?: AbortSignal) {
    return this.request<SteamWebApiKeySettings>('/v1/settings/steam-web-api-key', { signal });
  }

  updateSteamWebApiKey(input: SteamWebApiKeySettingsInput) {
    return this.request<SteamWebApiKeySettings>('/v1/settings/steam-web-api-key', { method: 'PUT', body: input });
  }

  getWarmupScheduling(signal?: AbortSignal) {
    return this.request<WarmupSchedulingSettings>('/v1/settings/warmup-scheduling', { signal });
  }

  updateWarmupScheduling(input: WarmupSchedulingSettingsInput) {
    return this.request<WarmupSchedulingSettings>('/v1/settings/warmup-scheduling', { method: 'PUT', body: input });
  }

  getWarmupPauseWindows(signal?: AbortSignal) {
    return this.request<WarmupPauseWindowsSettings>('/v1/settings/warmup-pause-windows', { signal });
  }

  updateWarmupPauseWindows(input: WarmupPauseWindowsSettingsInput) {
    return this.request<WarmupPauseWindowsSettings>('/v1/settings/warmup-pause-windows', { method: 'PUT', body: input });
  }

  queryLobby(lobbyId: string, signal?: AbortSignal) {
    return this.request<LobbySnapshot>(`/v1/lobbies/${encodeURIComponent(lobbyId)}`, { signal });
  }

  createServer(input: TargetServerInput) {
    return this.request<TargetServer>('/v1/servers', { method: 'POST', body: input });
  }

  updateServer(serverId: string, input: TargetServerInput) {
    return this.request<TargetServer>(`/v1/servers/${serverId}`, { method: 'PUT', body: input });
  }

  deleteServer(serverId: string) {
    return this.request<void>(`/v1/servers/${serverId}`, { method: 'DELETE' });
  }

  createAgent(input: WarmupAgentInput) {
    return this.request<WarmupAgent>('/v1/agents', { method: 'POST', body: input });
  }

  updateAgent(agentId: string, input: WarmupAgentInput) {
    return this.request<WarmupAgent>(`/v1/agents/${agentId}`, { method: 'PUT', body: input });
  }

  startAgent(agentId: string) {
    return this.request<WarmupAgent>(`/v1/agents/${agentId}/start`, { method: 'POST' });
  }

  stopAgent(agentId: string) {
    return this.request<WarmupAgent>(`/v1/agents/${agentId}/stop`, { method: 'POST' });
  }

  recreateAgent(agentId: string) {
    return this.request<WarmupAgent>(`/v1/agents/${agentId}/recreate`, { method: 'POST' });
  }

  deleteAgent(agentId: string) {
    return this.request<void>(`/v1/agents/${agentId}`, { method: 'DELETE' });
  }

  private async request<T>(path: string, options: RequestOptions): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (options.authenticated !== false) {
      const token = this.getToken();
      if (token) {
        headers.Authorization = `Bearer ${token}`;
      }
    }
    if (options.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    const response = await this.fetcher(path, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });

    if (response.status === 204) {
      return undefined as T;
    }
    if (!response.ok) {
      throw await CoreClient.toError(response);
    }
    return await response.json() as T;
  }

  private static async toError(response: Response): Promise<CoreApiError> {
    const body = await response.text();
    let code = `http_${response.status}`;
    try {
      const parsed: unknown = JSON.parse(body);
      if (typeof parsed === 'string') {
        code = parsed;
      }
    } catch {
      if (body.length > 0 && body.length < 160) {
        code = body;
      }
    }
    return new CoreApiError(response.status, code);
  }
}

interface RequestOptions {
  authenticated?: boolean;
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  signal?: AbortSignal;
}
