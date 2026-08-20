import {
  callable,
  definePlugin,
  DialogButton,
  Dropdown,
  Field,
  IconsModule,
  Millennium,
} from '@steambrew/client';
import React, { useEffect, useMemo, useState } from 'react';

const DOWNLOAD_REGION_FIELD = 8009;
const MAX_INT32 = 0x7fffffff;
const getBridgeConfig = callable<any>('get_region_bridge_config');
const recordRegionState = callable<any>('record_region_state');

type Region = { nRegionID: number; strRegionName: string };
type RegionInfo = { currentRegionId: number | null; regions: Region[] };

function encodeDownloadRegionSetting(regionId: number): string {
  if (!Number.isInteger(regionId) || regionId < 0 || regionId > MAX_INT32) {
    throw new Error('Download region ID must be an int32');
  }

  const bytes: number[] = [];
  let value = DOWNLOAD_REGION_FIELD * 8;
  do {
    let byte = value % 128;
    value = Math.floor(value / 128);
    if (value > 0) byte |= 0x80;
    bytes.push(byte);
  } while (value > 0);

  value = regionId;
  do {
    let byte = value % 128;
    value = Math.floor(value / 128);
    if (value > 0) byte |= 0x80;
    bytes.push(byte);
  } while (value > 0);

  return btoa(String.fromCharCode(...bytes));
}

function readSteamSettings(): RegionInfo {
  const store = (globalThis as any).settingsStore;
  const settings = store?.settings ?? {};
  const clientSettings = store?.clientSettings ?? {};
  const regions = Array.isArray(settings.vecValidDownloadRegions)
    ? settings.vecValidDownloadRegions.filter((region: Region) => Number.isInteger(region?.nRegionID))
    : [];
  const clientRegion = clientSettings.download_region;
  const settingsRegion = settings.download_region;
  const currentRegionId = Number.isInteger(clientRegion)
    ? clientRegion
    : Number.isInteger(settingsRegion)
      ? settingsRegion
      : null;
  return { currentRegionId, regions };
}

async function waitForSteamSettings(expectedRegionId: number | null = null): Promise<RegionInfo> {
  let info = readSteamSettings();
  for (let attempt = 0; attempt < 120; attempt += 1) {
    if (info.regions.length > 0 && info.currentRegionId !== null &&
      (expectedRegionId === null || info.currentRegionId === expectedRegionId)) return info;
    await new Promise((resolve) => setTimeout(resolve, 250));
    info = readSteamSettings();
  }
  return info;
}

async function synchronizeConfiguredRegion(): Promise<RegionInfo> {
  const config = await getBridgeConfig({});
  let info = await waitForSteamSettings();
  const targetRegionId = Number.isInteger(config?.targetRegionId) ? Number(config.targetRegionId) : null;
  if (targetRegionId !== null) {
    const target = info.regions.find((region) => region.nRegionID === targetRegionId);
    if (!target) throw new Error(`Steam download region ${targetRegionId} is not available`);
    if (info.currentRegionId !== targetRegionId) {
      await (globalThis as any).SteamClient.Settings.SetSetting(encodeDownloadRegionSetting(targetRegionId));
      info = await waitForSteamSettings(targetRegionId);
    }
  }

  if (info.currentRegionId !== null) {
    await recordRegionState({ regionId: info.currentRegionId });
  }
  return info;
}

let synchronizationPromise: Promise<RegionInfo> | null = null;

function synchronizeConfiguredRegionOnce(force = false): Promise<RegionInfo> {
  if (!force && synchronizationPromise) return synchronizationPromise;
  synchronizationPromise = synchronizeConfiguredRegion();
  return synchronizationPromise;
}

class SteamRegionBridgeApi {
  static async sync() {
    return synchronizeConfiguredRegionOnce(true);
  }
}

Millennium.exposeObj({ steamRegionBridge: SteamRegionBridgeApi });
void synchronizeConfiguredRegionOnce().catch((error) => {
  console.error('[steam-region-bridge] automatic synchronization failed', error);
});

function RegionBridge() {
  const [regionId, setRegionId] = useState<number | null>(null);
  const [regions, setRegions] = useState<Region[]>([]);
  const [status, setStatus] = useState('');

  const refresh = () => {
    const info = readSteamSettings();
    setRegions(info.regions);
    setRegionId(info.currentRegionId);
  };

  const synchronize = async () => {
    try {
      const info = await synchronizeConfiguredRegionOnce(true);
      setRegions(info.regions);
      setRegionId(info.currentRegionId);
      setStatus('Steam download region synchronized');
    } catch (error) {
      console.error('[steam-region-bridge] synchronization failed', error);
      setStatus('Steam download region synchronization failed');
      refresh();
    }
  };

  useEffect(() => {
    refresh();
    void synchronize();
  }, []);

  const options = useMemo(
    () => regions.map((region) => ({ data: region.nRegionID, label: region.strRegionName })),
    [regions],
  );

  const save = async (option: any) => {
    try {
      const nextId = Number(option.data);
      await (globalThis as any).SteamClient.Settings.SetSetting(encodeDownloadRegionSetting(nextId));
      await synchronizeConfiguredRegionOnce(true);
      refresh();
    } catch (error) {
      console.error('[steam-region-bridge] update failed', error);
      setStatus('Steam rejected the download region update');
    }
  };

  return (
    <>
      <Field label="Download region" description={status} bottomSeparator="standard">
        {options.length > 0 && regionId !== null ? (
          <Dropdown rgOptions={options} selectedOption={regionId} onChange={save} />
        ) : (
          <DialogButton onClick={synchronize}>Refresh Steam settings</DialogButton>
        )}
      </Field>
      <Field label="Current region ID" bottomSeparator="none">
        {regionId === null ? 'Unavailable' : String(regionId)}
      </Field>
    </>
  );
}

export default definePlugin(() => ({
  title: 'Steam Region Bridge',
  icon: <IconsModule.Settings />,
  content: <RegionBridge />,
}));
