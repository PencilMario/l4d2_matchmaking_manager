import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';

const mocks = vi.hoisted(() => ({
  fetchAllState: vi.fn(),
  fetchSteamDownloadRegions: vi.fn(),
  setAuthToken: vi.fn(),
  setUnauthorizedHandler: vi.fn(),
}));

vi.mock('./services/api', () => ({
  ApiService: mocks,
  getAuthToken: () => 'test-token',
  setAuthToken: mocks.setAuthToken,
  setUnauthorizedHandler: mocks.setUnauthorizedHandler,
}));

vi.mock('./services/steam-download-regions', () => ({
  loadSteamDownloadRegions: (fetchRegions: () => Promise<unknown>) => fetchRegions(),
}));

vi.mock('./components/PlayerEntryStatisticsView', () => ({
  PlayerEntryStatisticsView: () => <div>玩家进入统计挂载成功</div>,
}));

describe('App 统计页挂载', () => {
  beforeEach(() => {
    mocks.fetchAllState.mockReset().mockResolvedValue({
      targets: [],
      agents: [],
      attempts: [],
      observations: [],
      healthy: true,
    });
    mocks.fetchSteamDownloadRegions.mockReset().mockResolvedValue([]);
  });

  it('从现用 Sidebar 进入统计页并渲染统计视图', async () => {
    render(<App />);

    await screen.findByRole('button', { name: '进入统计' });
    fireEvent.click(screen.getByRole('button', { name: '进入统计' }));

    expect(await screen.findByText('玩家进入统计挂载成功')).toBeInTheDocument();
  });
});
