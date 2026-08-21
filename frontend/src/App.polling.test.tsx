import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';

const mocks = vi.hoisted(() => ({
  fetchAllState: vi.fn(),
  fetchSteamDownloadRegions: vi.fn(),
}));

vi.mock('./services/api', () => ({
  ApiService: mocks,
  getAuthToken: () => 'test-token',
  setAuthToken: vi.fn(),
  setUnauthorizedHandler: vi.fn(),
}));

vi.mock('./components/TargetServersView', () => ({
  TargetServersView: ({ onModalOpenChange }: { onModalOpenChange?: (open: boolean) => void }) => (
    <div>
      <button type="button" onClick={() => onModalOpenChange?.(true)}>打开服务器设置</button>
      <button type="button" onClick={() => onModalOpenChange?.(false)}>关闭服务器设置</button>
    </div>
  ),
}));

vi.mock('./components/WarmupAgentsView', () => ({
  WarmupAgentsView: ({ onModalOpenChange }: { onModalOpenChange?: (open: boolean) => void }) => (
    <button type="button" onClick={() => onModalOpenChange?.(true)}>打开暖服节点设置</button>
  ),
}));

const state = {
  targets: [],
  agents: [],
  attempts: [],
  observations: [],
  healthy: true,
};

describe('App 后台轮询', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mocks.fetchAllState.mockReset().mockResolvedValue(state);
    mocks.fetchSteamDownloadRegions.mockReset().mockResolvedValue([]);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('服务器设置弹窗打开时暂停 5 秒刷新，关闭后立即恢复刷新', async () => {
    render(<App />);
    await act(async () => {
      await Promise.resolve();
    });
    expect(mocks.fetchAllState).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: '目标服务器' }));
    fireEvent.click(screen.getByRole('button', { name: '打开服务器设置' }));

    await act(async () => {
      vi.advanceTimersByTime(5000);
      await Promise.resolve();
    });
    expect(mocks.fetchAllState).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: '关闭服务器设置' }));
    await act(async () => {
      await Promise.resolve();
    });
    expect(mocks.fetchAllState).toHaveBeenCalledTimes(2);
  });

  it('暖服节点设置弹窗打开时也暂停 5 秒刷新', async () => {
    render(<App />);
    await act(async () => {
      await Promise.resolve();
    });
    expect(mocks.fetchAllState).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: '暖服节点' }));
    fireEvent.click(screen.getByRole('button', { name: '打开暖服节点设置' }));

    await act(async () => {
      vi.advanceTimersByTime(5000);
      await Promise.resolve();
    });
    expect(mocks.fetchAllState).toHaveBeenCalledTimes(1);
  });
});
