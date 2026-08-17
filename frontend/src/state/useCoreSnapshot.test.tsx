import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useCoreSnapshot } from './useCoreSnapshot';

describe('useCoreSnapshot', () => {
  it('loads independent Core resources in one initial snapshot', async () => {
    const client = {
      listServers: vi.fn().mockResolvedValue([]),
      listAgents: vi.fn().mockResolvedValue([]),
      listWarmups: vi.fn().mockResolvedValue([]),
      listServerObservations: vi.fn().mockResolvedValue([]),
    };

    const { result } = renderHook(() => useCoreSnapshot(client));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(client.listServers).toHaveBeenCalledTimes(1);
    expect(client.listAgents).toHaveBeenCalledTimes(1);
    expect(client.listWarmups).toHaveBeenCalledTimes(1);
    expect(client.listServerObservations).toHaveBeenCalledTimes(1);
    expect(result.current.stale).toBe(false);
  });
});
