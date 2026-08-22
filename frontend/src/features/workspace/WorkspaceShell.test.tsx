import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { WorkspaceShell } from './WorkspaceShell';

const mocks = vi.hoisted(() => ({
  snapshot: {
    servers: [],
    agents: [],
    warmups: [],
    observations: [],
    loading: false,
    stale: false,
    error: null,
    refreshedAt: new Date('2026-08-20T10:00:00Z'),
    refresh: vi.fn(),
  },
}));

vi.mock('../../state/useCoreSnapshot', () => ({
  useCoreSnapshot: () => mocks.snapshot,
}));

vi.mock('../settings/SettingsWorkspace', () => ({
  SettingsWorkspace: ({
    updateWarmupScheduling,
    updateWarmupPauseWindows,
  }: {
    updateWarmupScheduling?: (input: { enabled: boolean }) => Promise<unknown>;
    updateWarmupPauseWindows?: (input: { windows: { start: string; end: string }[] }) => Promise<unknown>;
  }) => <>
    <button type="button" onClick={() => void updateWarmupScheduling?.({ enabled: false })}>
      触发暖服调度更新
    </button>
    <button type="button" onClick={() => void updateWarmupPauseWindows?.({ windows: [{ start: '23:00', end: '00:00' }] })}>
      触发暂停时间段更新
    </button>
  </>,
}));

describe('工作台设置 wiring', () => {
  it('passes the dedicated warmup scheduling updater to the settings workspace', async () => {
    const updateWarmupScheduling = vi.fn().mockResolvedValue({ enabled: false, updatedAt: '2026-08-20T10:00:00Z' });
    const client = {
      listServers: vi.fn(),
      listAgents: vi.fn(),
      listWarmups: vi.fn(),
      listServerObservations: vi.fn(),
      getSettings: vi.fn(),
    updateVncProxy: vi.fn(),
    updateSteamWebApiKey: vi.fn(),
    updateWarmupScheduling,
    updateWarmupPauseWindows: vi.fn().mockResolvedValue({ windows: [{ start: '23:00', end: '00:00' }], active: false, updatedAt: '2026-08-20T10:00:00Z' }),
  };

    render(<WorkspaceShell client={client} />);
    fireEvent.click(screen.getByRole('button', { name: '全局设置' }));
    fireEvent.click(screen.getByRole('button', { name: '触发暖服调度更新' }));

    expect(updateWarmupScheduling).toHaveBeenCalledWith({ enabled: false });
  });

  it('passes the dedicated warmup pause updater to the settings workspace', async () => {
    const updateWarmupPauseWindows = vi.fn().mockResolvedValue({ windows: [], active: false, updatedAt: '2026-08-20T10:00:00Z' });
    const client = {
      listServers: vi.fn(),
      listAgents: vi.fn(),
      listWarmups: vi.fn(),
      listServerObservations: vi.fn(),
      getSettings: vi.fn(),
      updateVncProxy: vi.fn(),
      updateSteamWebApiKey: vi.fn(),
      updateWarmupScheduling: vi.fn(),
      updateWarmupPauseWindows,
    };

    render(<WorkspaceShell client={client} />);
    fireEvent.click(screen.getByRole('button', { name: '全局设置' }));
    fireEvent.click(screen.getByRole('button', { name: '触发暂停时间段更新' }));

    expect(updateWarmupPauseWindows).toHaveBeenCalledWith({ windows: [{ start: '23:00', end: '00:00' }] });
  });
});
