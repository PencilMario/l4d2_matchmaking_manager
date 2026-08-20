import { describe, expect, it } from 'vitest';
import { decodeDownloadRegionSetting, encodeDownloadRegionSetting } from './steam-region-protocol';

describe('Steam download region setting protocol', () => {
  it('encodes the official CMsgClientSettings field', () => {
    expect(encodeDownloadRegionSetting(0)).toBe('yPQDAA==');
  });

  it('round-trips a positive region id', () => {
    expect(decodeDownloadRegionSetting(encodeDownloadRegionSetting(4))).toBe(4);
  });

  it('rejects invalid region ids', () => {
    expect(() => encodeDownloadRegionSetting(-1)).toThrow();
    expect(() => encodeDownloadRegionSetting(2 ** 31)).toThrow();
  });
});
