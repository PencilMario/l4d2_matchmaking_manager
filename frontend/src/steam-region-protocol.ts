const DOWNLOAD_REGION_FIELD = 8009;
const MAX_INT32 = 0x7fffffff;

function encodeVarint(value: number): number[] {
  const bytes: number[] = [];
  do {
    let byte = value % 128;
    value = Math.floor(value / 128);
    if (value > 0) byte |= 0x80;
    bytes.push(byte);
  } while (value > 0);
  return bytes;
}

function decodeVarint(bytes: number[]): { value: number; consumed: number } {
  let value = 0;
  let multiplier = 1;
  for (let index = 0; index < bytes.length; index += 1) {
    const byte = bytes[index];
    value += (byte & 0x7f) * multiplier;
    if ((byte & 0x80) === 0) return { value, consumed: index + 1 };
    multiplier *= 128;
  }
  throw new Error('Malformed protobuf varint');
}

export function encodeDownloadRegionSetting(regionId: number): string {
  if (!Number.isInteger(regionId) || regionId < 0 || regionId > MAX_INT32) {
    throw new Error('Download region ID must be an int32');
  }

  const bytes = [...encodeVarint(DOWNLOAD_REGION_FIELD << 3), ...encodeVarint(regionId)];
  return btoa(String.fromCharCode(...bytes));
}

export function decodeDownloadRegionSetting(base64: string): number {
  const bytes = Array.from(atob(base64), (character) => character.charCodeAt(0));
  const tag = decodeVarint(bytes);
  if (tag.value !== DOWNLOAD_REGION_FIELD << 3) throw new Error('Unexpected Steam setting field');
  return decodeVarint(bytes.slice(tag.consumed)).value;
}
