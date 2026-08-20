import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  loadSteamDownloadRegions,
  STEAM_DOWNLOAD_REGIONS_CACHE_KEY,
  STEAM_DOWNLOAD_REGIONS_TTL_MS,
} from './steam-download-regions';

const regions = [{ id: 168, name: '中国 - 青岛' }];

describe('loadSteamDownloadRegions', () => {
  beforeEach(() => localStorage.clear());

  it('uses a fresh browser cache without requesting the Core API', async () => {
    localStorage.setItem(STEAM_DOWNLOAD_REGIONS_CACHE_KEY, JSON.stringify({ fetchedAt: 100, regions }));
    const fetchRegions = vi.fn();

    await expect(loadSteamDownloadRegions(fetchRegions, localStorage, () => 100 + STEAM_DOWNLOAD_REGIONS_TTL_MS - 1))
      .resolves.toEqual(regions);

    expect(fetchRegions).not.toHaveBeenCalled();
  });

  it('refreshes an expired cache and stores the new directory', async () => {
    localStorage.setItem(STEAM_DOWNLOAD_REGIONS_CACHE_KEY, JSON.stringify({ fetchedAt: 100, regions: [] }));
    const refreshed = [{ id: 47, name: '中国 - 上海' }];
    const fetchRegions = vi.fn().mockResolvedValue(refreshed);

    await expect(loadSteamDownloadRegions(fetchRegions, localStorage, () => 100 + STEAM_DOWNLOAD_REGIONS_TTL_MS))
      .resolves.toEqual(refreshed);

    expect(fetchRegions).toHaveBeenCalledTimes(1);
    expect(JSON.parse(localStorage.getItem(STEAM_DOWNLOAD_REGIONS_CACHE_KEY)!)).toEqual({
      fetchedAt: 100 + STEAM_DOWNLOAD_REGIONS_TTL_MS,
      regions: refreshed,
    });
  });

  it('falls back to expired data when the Core API is unavailable', async () => {
    localStorage.setItem(STEAM_DOWNLOAD_REGIONS_CACHE_KEY, JSON.stringify({ fetchedAt: 100, regions }));
    const fetchRegions = vi.fn().mockRejectedValue(new Error('offline'));

    await expect(loadSteamDownloadRegions(fetchRegions, localStorage, () => 100 + STEAM_DOWNLOAD_REGIONS_TTL_MS))
      .resolves.toEqual(regions);
  });

  it('fails without cache when the Core API is unavailable', async () => {
    const failure = new Error('offline');

    await expect(loadSteamDownloadRegions(vi.fn().mockRejectedValue(failure), localStorage)).rejects.toBe(failure);
  });
});
