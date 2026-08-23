import { describe, expect, it } from 'vitest';
import { formatStatisticsDownloadRegion, formatSteamDownloadRegion, normalizeSteamDownloadRegion } from './steam-download-region-display';

const regions = [
  { id: 32, name: '日本 - 东京' },
  { id: 47, name: '中国 - 上海' },
  { id: 168, name: '中国 - 青岛' },
];

describe('Steam download region display', () => {
  it('converts legacy agent values to the internal region ID', () => {
    expect(normalizeSteamDownloadRegion('cng')).toBe('47');
    expect(normalizeSteamDownloadRegion('hongkong')).toBe('33');
    expect(normalizeSteamDownloadRegion('168')).toBe('168');
  });

  it('displays the directory name instead of a legacy code or numeric ID', () => {
    expect(formatSteamDownloadRegion('cng', regions)).toBe('中国 - 上海');
    expect(formatSteamDownloadRegion('168', regions)).toBe('中国 - 青岛');
    expect(formatSteamDownloadRegion(undefined, regions)).toBe('--');
  });

  it('displays statistics region codes as directory names and null as default', () => {
    expect(formatStatisticsDownloadRegion('47', regions)).toBe('中国 - 上海');
    expect(formatStatisticsDownloadRegion('168', regions)).toBe('中国 - 青岛');
    expect(formatStatisticsDownloadRegion(null, regions)).toBe('默认');
  });
});
