import type { SteamDownloadRegion } from '../types';

const LEGACY_REGION_IDS: Record<string, string> = {
  hongkong: '33',
  shanghai: '47',
  cng: '47',
  qingdao: '168',
  tokyo: '32',
};

export function normalizeSteamDownloadRegion(value?: string | null): string | undefined {
  const normalized = value?.trim();
  if (!normalized) return undefined;
  return LEGACY_REGION_IDS[normalized.toLowerCase()] ?? normalized;
}

export function formatSteamDownloadRegion(
  value: string | undefined,
  regions: readonly SteamDownloadRegion[],
): string {
  const normalized = normalizeSteamDownloadRegion(value);
  if (!normalized) return '--';
  return regions.find((region) => String(region.id) === normalized)?.name ?? '未知区域';
}

export function formatStatisticsDownloadRegion(
  value: string | null | undefined,
  regions: readonly SteamDownloadRegion[],
): string {
  if (value == null || value.trim() === '') return '默认';
  return formatSteamDownloadRegion(value, regions);
}
