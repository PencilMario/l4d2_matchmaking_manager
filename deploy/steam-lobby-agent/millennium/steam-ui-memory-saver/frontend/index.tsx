import { EUIMode, callable, definePlugin, IconsModule, routerHook } from '@steambrew/client';
import React, { useEffect } from 'react';

const getMemorySaverConfig = callable<any>('get_steam_ui_memory_saver_config');
const libraryRoutes = ['/library', '/library/downloads'];

type MemorySaverConfig = {
  enabled?: boolean;
};

function decodeConfig(value: unknown): MemorySaverConfig {
  if (typeof value !== 'string') return (value ?? {}) as MemorySaverConfig;
  try {
    return JSON.parse(value) as MemorySaverConfig;
  } catch {
    return {};
  }
}

function EmptySteamRoute(): null {
  return null;
}

function replaceRoute(route: any) {
  return {
    ...route,
    children: React.createElement(EmptySteamRoute),
  };
}

let patchesInstalled = false;
let activationPromise: Promise<boolean> | null = null;

async function installRoutePatches(): Promise<boolean> {
  if (patchesInstalled) return true;
  if (activationPromise) return activationPromise;

  activationPromise = (async () => {
    const config = decodeConfig(await getMemorySaverConfig({}));
    if (config.enabled !== true) return false;

    libraryRoutes.forEach((path) => {
      routerHook.addPatch(path, replaceRoute, EUIMode.Desktop);
    });
    patchesInstalled = true;
    console.info('[steam-ui-memory-saver] desktop Library routes unloaded');
    return true;
  })();

  try {
    return await activationPromise;
  } finally {
    activationPromise = null;
  }
}

function MemorySaverRuntime(): null {
  useEffect(() => {
    const timer = setInterval(() => {
      void installRoutePatches().catch((error) => {
        console.error('[steam-ui-memory-saver] readiness check failed', error);
      });
    }, 5000);
    return () => clearInterval(timer);
  }, []);

  return null;
}

void installRoutePatches().catch((error) => {
  console.error('[steam-ui-memory-saver] initial readiness check failed', error);
});

export default definePlugin(() => ({
  title: 'Steam UI Memory Saver',
  icon: <IconsModule.Settings />,
  content: <MemorySaverRuntime />,
}));
