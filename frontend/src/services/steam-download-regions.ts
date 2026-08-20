import type { SteamDownloadRegion } from '../types';

export const STEAM_DOWNLOAD_REGIONS_CACHE_KEY = 'steam-download-regions:v1';
export const STEAM_DOWNLOAD_REGIONS_TTL_MS = 24 * 60 * 60 * 1000;

type CachedRegions = {
  fetchedAt: number;
  regions: SteamDownloadRegion[];
};

type RegionFetcher = () => Promise<SteamDownloadRegion[]>;

function isValidCache(value: unknown): value is CachedRegions {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<CachedRegions>;
  return Number.isFinite(candidate.fetchedAt) && Array.isArray(candidate.regions) && candidate.regions.every(
    (region) => region && Number.isInteger(region.id) && typeof region.name === 'string',
  );
}

function readCache(storage: Storage): CachedRegions | null {
  try {
    const raw = storage.getItem(STEAM_DOWNLOAD_REGIONS_CACHE_KEY);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    return isValidCache(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function writeCache(storage: Storage, value: CachedRegions) {
  try {
    storage.setItem(STEAM_DOWNLOAD_REGIONS_CACHE_KEY, JSON.stringify(value));
  } catch {
    // A disabled or full browser storage must not prevent the directory from loading.
  }
}

export async function loadSteamDownloadRegions(
  fetchRegions: RegionFetcher,
  storage: Storage = localStorage,
  now: () => number = Date.now,
): Promise<SteamDownloadRegion[]> {
  const cached = readCache(storage);
  if (cached && now() - cached.fetchedAt < STEAM_DOWNLOAD_REGIONS_TTL_MS) {
    return cached.regions;
  }

  try {
    const regions = await fetchRegions();
    const next = { fetchedAt: now(), regions };
    writeCache(storage, next);
    return regions;
  } catch (error) {
    if (cached) return cached.regions;
    throw error;
  }
}
