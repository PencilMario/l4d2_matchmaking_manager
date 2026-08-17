import { expect, test } from '@playwright/test';

const servers = [
  { id: 'online', endpoint: '203.0.113.10:27015', requiresReservation: true, priority: 5, maxConcurrentWarmups: 1, attemptWindowSeconds: 180, playerTarget: 8, enabled: true, hasRconCredentials: true, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' },
  { id: 'unavailable', endpoint: '203.0.113.11:27015', requiresReservation: false, priority: 0, maxConcurrentWarmups: 2, attemptWindowSeconds: 180, playerTarget: 8, enabled: true, hasRconCredentials: false, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' },
  { id: 'pending', endpoint: '203.0.113.12:27015', requiresReservation: false, priority: 0, maxConcurrentWarmups: 2, attemptWindowSeconds: 180, playerTarget: 8, enabled: true, hasRconCredentials: false, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' },
];

const observations = [
  { targetServerId: 'online', status: 'online', serverName: 'L4D2 HK Versus #1', playerCount: 2, maxPlayers: 12, observedAt: '2026-08-18T12:00:00Z' },
  { targetServerId: 'unavailable', status: 'unavailable', serverName: null, playerCount: null, maxPlayers: null, observedAt: '2026-08-18T12:00:00Z' },
  { targetServerId: 'pending', status: 'pending', serverName: null, playerCount: null, maxPlayers: null, observedAt: null },
];

const completeLobby = { lobbyId: '109775242425650097', ownerSteamId: '76561198000000001', members: [{ steamId: '76561198000000002', personaName: 'Player One' }], metadata: { 'Game:state': 'game' }, observedAt: '2026-08-18T12:00:00Z', memberDataStatus: 'complete' };

test.beforeEach(async ({ page }) => {
  await page.route('**/healthz', route => route.fulfill({ json: { status: 'alive' } }));
  await page.route(/\/v1\/servers(?:\/.*)?$/, async route => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    if (pathname === '/v1/servers/observations') {
      await route.fulfill({ json: observations });
      return;
    }
    if (pathname === '/v1/servers' && request.method() === 'GET') {
      await route.fulfill({ json: servers });
      return;
    }
    if (pathname === '/v1/servers/online' && request.method() === 'PUT') {
      await route.fulfill({ status: 409, contentType: 'application/json', body: '"target_server_drain_failed"' });
      return;
    }
    await route.fulfill({ status: 404, contentType: 'application/json', body: '"mock_route_not_found"' });
  });
  await page.route('**/v1/agents', route => route.fulfill({ json: [{ id: 'agent-q', name: 'hk-quarantine', status: 'quarantined', downloadRegion: null, noVncPort: 18083, createdAt: '2026-08-18T12:00:00Z', updatedAt: '2026-08-18T12:00:00Z' }] }));
  await page.route('**/v1/warmups', route => route.fulfill({ json: [{ targetServerId: 'online', targetEndpoint: '203.0.113.10:27015', warmupAgentId: 'agent-q', warmupAgentName: 'hk-quarantine', operationId: 'warmup-1', lobbyId: null, mode: 'versus', state: 'active', phase: 'awaiting_external_members', startedAt: '2026-08-18T12:00:00Z', lobbyReadyAt: null, firstExternalMemberAt: null, quietSince: null, observedAt: '2026-08-18T12:00:00Z', deadline: '2026-08-18T12:03:00Z', remainingSeconds: 120 }] }));
  await page.route('**/v1/lobbies/*', route => {
    const lobbyId = route.request().url().split('/').at(-1);
    return route.fulfill({ json: lobbyId === '109775242425650098' ? { ...completeLobby, lobbyId, members: [], memberDataStatus: 'metadata_only_join_timeout' } : completeLobby });
  });
});

test('runs the desktop A2S management journey without visual-plane interference', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('CORE API TOKEN').fill('test-token');
  await page.getByRole('button', { name: '连接 Core' }).click();

  await expect(page.getByText('awaiting_external_members')).toBeVisible();
  await expect(page.getByRole('button', { name: '筛选 203.0.113.11:27015' })).toContainText('A2S 不可用');
  await expect(page.getByRole('button', { name: '筛选 203.0.113.12:27015' })).toContainText('等待首次观测');
  await page.getByRole('button', { name: '筛选 203.0.113.10:27015' }).click();
  await expect(page.getByText('L4D2 HK Versus #1')).toBeVisible();
  await expect(page.getByText('2 / 12')).toBeVisible();
  await expect(page.getByRole('button', { name: '配置 203.0.113.10:27015' })).toHaveCount(1);

  await page.getByRole('button', { name: '清除拓扑筛选' }).click();
  await expect(page.locator('[data-server-row="unavailable"]')).toContainText('A2S 不可用');
  await expect(page.locator('[data-server-row="pending"]')).toContainText('等待首次观测');
  await page.getByRole('button', { name: '配置 203.0.113.10:27015' }).click();
  await page.getByRole('button', { name: '编辑配置', exact: true }).click();
  await page.getByLabel('启用调度').uncheck();
  await page.getByRole('button', { name: '保存配置' }).click();
  await page.getByRole('button', { name: '继续' }).click();
  await page.getByRole('button', { name: '确认禁用' }).click();
  await expect(page.getByText('target_server_drain_failed')).toBeVisible();
  await expect(page.getByRole('form', { name: 'Target Server 配置表单' })).toBeVisible();

  await page.getByRole('button', { name: '关闭配置抽屉' }).click();
  await page.getByRole('button', { name: 'Lobby 查询' }).click();
  await page.getByLabel('Lobby ID').fill('109775242425650097');
  await page.getByRole('button', { name: '查询 Lobby' }).click();
  await expect(page.getByText('确认成员 1')).toBeVisible();
  await page.getByLabel('Lobby ID').fill('109775242425650098');
  await page.getByRole('button', { name: '查询 Lobby' }).click();
  await expect(page.getByText('成员数据未确认')).toBeVisible();
  await expect(page.getByText('成员数 不可用')).toBeVisible();

  const visualLayer = page.locator('[data-visual-stage]');
  await expect(visualLayer).toBeVisible();
  const layerState = await visualLayer.evaluate(element => {
    const style = window.getComputedStyle(element);
    const fallback = element.querySelector('.visual-stage__fallback');
    return { dataPlaneZ: window.getComputedStyle(document.querySelector('[data-workspace-data]')!).zIndex, fallbackColor: fallback ? window.getComputedStyle(fallback).backgroundColor : null, pointerEvents: style.pointerEvents, visualZ: style.zIndex };
  });
  expect(layerState.pointerEvents).toBe('none');
  expect(Number(layerState.visualZ)).toBeLessThan(Number(layerState.dataPlaneZ));
  expect(layerState.fallbackColor).not.toBe('rgba(0, 0, 0, 0)');

  const visualCanvas = visualLayer.locator('canvas');
  await expect(visualCanvas).toBeVisible();
  expect((await visualCanvas.screenshot()).byteLength).toBeGreaterThan(1_000);
  const canvasSize = await visualCanvas.evaluate(element => {
    const canvas = element as HTMLCanvasElement;
    return { height: canvas.height, width: canvas.width };
  });
  expect(canvasSize.width).toBeGreaterThan(0);
  expect(canvasSize.height).toBeGreaterThan(0);

  const overflow = await page.locator('.target-server-row__identity, .target-server-row__schedule, .warmup-queue__row').evaluateAll(elements => elements.map(element => ({ clientWidth: element.clientWidth, scrollWidth: element.scrollWidth })));
  expect(overflow.every(item => item.scrollWidth <= item.clientWidth)).toBe(true);
});
