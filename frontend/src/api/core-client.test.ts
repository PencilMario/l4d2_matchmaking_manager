import { describe, expect, it, vi } from 'vitest';
import { CoreClient } from './core-client';

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

describe('CoreClient', () => {
  it('sends the bearer token and parses direct arrays', async () => {
    const fetcher = vi.fn().mockResolvedValue(jsonResponse([]));
    const client = new CoreClient(() => 'test-token', fetcher);

    const servers = await client.listServers();

    expect(servers).toEqual([]);
    expect(fetcher).toHaveBeenCalledWith('/v1/servers', expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
    }));
  });

  it('normalizes JSON-string errors while retaining a conflict code', async () => {
    const client = new CoreClient(
      () => 'test-token',
      vi.fn().mockResolvedValue(jsonResponse('target_server_drain_failed', 409)),
    );

    await expect(client.listServers()).rejects.toMatchObject({
      status: 409,
      code: 'target_server_drain_failed',
      retryable: false,
    });
  });

  it('does not parse a 204 response body', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    const client = new CoreClient(() => 'test-token', fetcher);

    await expect(client.deleteServer('server-id')).resolves.toBeUndefined();
  });

  it('reads all observations from the dedicated bulk route', async () => {
    const fetcher = vi.fn().mockResolvedValue(jsonResponse([]));
    const client = new CoreClient(() => 'test-token', fetcher);

    await client.listServerObservations();

    expect(fetcher).toHaveBeenCalledTimes(1);
    expect(fetcher).toHaveBeenCalledWith('/v1/servers/observations', expect.any(Object));
  });

  it('uses dedicated endpoints for VNC proxy and Steam Web API key updates', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ proxyUrl: 'http://127.0.0.1:7890/', updatedAt: '2026-08-20T00:00:00Z' }))
      .mockResolvedValueOnce(jsonResponse({ configured: true, updatedAt: '2026-08-20T00:00:01Z' }));
    const client = new CoreClient(() => 'test-token', fetcher);

    await client.updateVncProxy({ proxyUrl: 'http://127.0.0.1:7890' });
    await client.updateSteamWebApiKey({ apiKey: 'secret' });

    expect(fetcher).toHaveBeenNthCalledWith(1, '/v1/settings/vnc-proxy', expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ proxyUrl: 'http://127.0.0.1:7890' }),
    }));
    expect(fetcher).toHaveBeenNthCalledWith(2, '/v1/settings/steam-web-api-key', expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ apiKey: 'secret' }),
    }));
  });

  it('uses the dedicated endpoint for global warmup scheduling', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ enabled: false, updatedAt: '2026-08-20T00:00:00Z' }))
      .mockResolvedValueOnce(jsonResponse({ enabled: true, updatedAt: '2026-08-20T00:00:01Z' }));
    const client = new CoreClient(() => 'test-token', fetcher);

    await client.getWarmupScheduling();
    await client.updateWarmupScheduling({ enabled: true });

    expect(fetcher).toHaveBeenNthCalledWith(1, '/v1/settings/warmup-scheduling', expect.objectContaining({
      method: 'GET',
    }));
    expect(fetcher).toHaveBeenNthCalledWith(2, '/v1/settings/warmup-scheduling', expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ enabled: true }),
    }));
  });

  it('calls the browser fetch with its global context by default', async () => {
    const browserFetch = vi.fn(function (this: typeof globalThis) {
      expect(this).toBe(globalThis);
      return Promise.resolve(jsonResponse([]));
    });
    vi.stubGlobal('fetch', browserFetch);

    try {
      const client = new CoreClient(() => 'test-token');

      await client.listServers();
    } finally {
      vi.unstubAllGlobals();
    }
  });
});
