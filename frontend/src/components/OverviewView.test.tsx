import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { OverviewView } from './OverviewView';

describe('系统概览大厅 ID', () => {
  it('以普通文本显示大厅 ID', () => {
    render(
      <OverviewView
        targets={[]}
        agents={[]}
        observations={[]}
        attempts={[{
          id: 'attempt-1', targetServerId: 'server-1', targetServerEndpoint: '127.0.0.1:27015',
          agentId: 'agent-1', agentName: 'agent-1', operationMode: 'standard', status: 'active',
          phase: 'observing_state', lobbyId: '109775242226986793', remainingSeconds: 30,
          totalSeconds: 720, startedAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
        }]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    expect(screen.getByText('109775242226986793')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '复制大厅 ID 109775242226986793' })).not.toBeInTheDocument();
  });

  it('按目标服务器聚合并展示多个暖服节点', () => {
    render(
      <OverviewView
        targets={[{
          id: 'server-1',
          endpoint: '127.0.0.1:27015',
          name: '服务器[1]',
          currentPlayers: 0,
          maxPlayers: 12,
          a2sStatus: 'online',
          enabled: true,
          priority: 0,
          requiresReservation: false,
          maxConcurrentWarmups: 36,
          activeWarmupsCount: 2,
          attemptWindowSeconds: 720,
          playerTarget: 6,
          createdAt: '2026-08-19T00:00:00Z',
          updatedAt: '2026-08-19T00:00:00Z',
        }]}
        agents={[{
          id: 'agent-1',
          name: '暖服节点-A',
          status: 'running',
          steamRegion: 'hongkong',
          keepVncAlive: true,
          novncPort: 18083,
          activeAttemptsCount: 1,
          createdAt: '2026-08-19T00:00:00Z',
          updatedAt: '2026-08-19T00:00:00Z',
        }]}
        observations={[]}
        attempts={[
          {
            id: 'attempt-1',
            targetServerId: 'server-1',
            targetServerEndpoint: '127.0.0.1:27015',
            agentId: 'agent-1',
            agentName: '暖服节点-A',
            operationMode: 'standard',
            status: 'active',
            phase: 'awaiting_ready',
            lobbyId: '109775242226986793',
            remainingSeconds: 60,
            totalSeconds: 720,
            startedAt: '2026-08-19T00:00:00Z',
            updatedAt: '2026-08-19T00:00:00Z',
          },
          {
            id: 'attempt-2',
            targetServerId: 'server-1',
            targetServerEndpoint: '127.0.0.1:27015',
            agentId: 'agent-2',
            agentName: '暖服节点-B',
            operationMode: 'standard',
            status: 'active',
            phase: 'awaiting_ready',
            lobbyId: '109775242226986794',
            remainingSeconds: 30,
            totalSeconds: 720,
            startedAt: '2026-08-19T00:01:00Z',
            updatedAt: '2026-08-19T00:01:00Z',
          },
        ]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    expect(screen.getAllByText('服务器[1]')).toHaveLength(1);
    expect(screen.getByText('2 个节点正在暖服')).toBeInTheDocument();
    expect(screen.getByText('0 / 12')).toBeInTheDocument();
    expect(screen.getByText('暖服节点-A')).toBeInTheDocument();
    expect(screen.getByText('暖服节点-B')).toBeInTheDocument();
    expect(screen.getByText('109775242226986793')).toBeInTheDocument();
    expect(screen.getByText('109775242226986794')).toBeInTheDocument();
    expect(screen.getByText('Steam 区域: hongkong')).toBeInTheDocument();
    expect(screen.queryByText('端口: 18083')).not.toBeInTheDocument();
  });

  it('支持层级展开、搜索筛选，并显示没有暖服任务的目标服务器', async () => {
    const user = userEvent.setup();
    render(
      <OverviewView
        targets={[
          {
            id: 'server-1', endpoint: '127.0.0.1:27015', name: '在线服务器',
            currentPlayers: 2, maxPlayers: 12, a2sStatus: 'online', enabled: true,
            priority: 0, requiresReservation: false, maxConcurrentWarmups: 2,
            activeWarmupsCount: 1, attemptWindowSeconds: 720, playerTarget: 6,
            createdAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
          },
          {
            id: 'server-2', endpoint: '127.0.0.1:27016', name: '空闲服务器',
            currentPlayers: 0, maxPlayers: 12, a2sStatus: 'online', enabled: true,
            priority: 1, requiresReservation: false, maxConcurrentWarmups: 2,
            activeWarmupsCount: 0, attemptWindowSeconds: 720, playerTarget: 6,
            createdAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
          },
        ]}
        agents={[]}
        observations={[]}
        attempts={[{
          id: 'attempt-1', targetServerId: 'server-1', targetServerEndpoint: '127.0.0.1:27015',
          agentId: 'agent-1', agentName: '节点-alpha', operationMode: 'standard', status: 'active',
          phase: 'awaiting_ready', lobbyId: 'lobby-1', remainingSeconds: 90, totalSeconds: 720,
          startedAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
        }]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    expect(screen.getByText('空闲服务器')).toBeInTheDocument();
    expect(screen.getByText('在线服务器')).toBeInTheDocument();
    expect(screen.getByText('服务器暖服剩余时间')).toBeInTheDocument();
    expect(screen.getByText('服务器暖服开始时间')).toBeInTheDocument();
    expect(screen.queryByText('节点开始时间')).not.toBeInTheDocument();
    expect(screen.getByText('节点-alpha')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '折叠节点列表' }));
    expect(screen.queryByText('节点-alpha')).not.toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: '展开节点列表' })[0]);
    expect(screen.getByText('节点-alpha')).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText('搜索服务器 / 节点 / 大厅'), '空闲');
    expect(screen.getByText('空闲服务器')).toBeInTheDocument();
    expect(screen.queryByText('在线服务器')).not.toBeInTheDocument();
  });

  it('按正在暖服筛选服务器，并支持全部展开和折叠', async () => {
    const user = userEvent.setup();
    render(
      <OverviewView
        targets={[
          {
            id: 'server-1', endpoint: '127.0.0.1:27015', name: '暖服服务器', currentPlayers: 0,
            maxPlayers: 12, a2sStatus: 'online', enabled: true, priority: 0,
            requiresReservation: false, maxConcurrentWarmups: 2, activeWarmupsCount: 1,
            attemptWindowSeconds: 720, playerTarget: 6, createdAt: '2026-08-19T00:00:00Z',
            updatedAt: '2026-08-19T00:00:00Z',
          },
          {
            id: 'server-2', endpoint: '127.0.0.1:27016', name: '空闲服务器', currentPlayers: 0,
            maxPlayers: 12, a2sStatus: 'online', enabled: true, priority: 0,
            requiresReservation: false, maxConcurrentWarmups: 2, activeWarmupsCount: 0,
            attemptWindowSeconds: 720, playerTarget: 6, createdAt: '2026-08-19T00:00:00Z',
            updatedAt: '2026-08-19T00:00:00Z',
          },
        ]}
        agents={[]}
        observations={[]}
        attempts={[{
          id: 'attempt-1', targetServerId: 'server-1', targetServerEndpoint: '127.0.0.1:27015',
          agentId: 'agent-1', agentName: '节点-alpha', operationMode: 'standard', status: 'active',
          phase: 'awaiting_ready', remainingSeconds: 90, totalSeconds: 720,
          startedAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
        }]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    await user.click(screen.getByRole('button', { name: /正在暖服/ }));
    expect(screen.getByText('暖服服务器')).toBeInTheDocument();
    expect(screen.queryByText('空闲服务器')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '全部展开' }));
    await user.click(screen.getByRole('button', { name: '全部折叠' }));
    expect(screen.queryByText('节点-alpha')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: '全部展开' }));
    expect(screen.getByText('节点-alpha')).toBeInTheDocument();
  });

  it('不可用服务器提示使用通用游戏端口文案', () => {
    render(
      <OverviewView
        targets={[{
          id: 'server-unavailable', endpoint: '10.0.0.8:27016', name: '不可用服务器',
          currentPlayers: 0, maxPlayers: 12, a2sStatus: 'unavailable', enabled: true,
          priority: 0, requiresReservation: false, maxConcurrentWarmups: 2,
          activeWarmupsCount: 0, attemptWindowSeconds: 720, playerTarget: 6,
          createdAt: '2026-08-19T00:00:00Z', updatedAt: '2026-08-19T00:00:00Z',
        }]}
        agents={[]}
        observations={[]}
        attempts={[]}
        onNavigateToTarget={vi.fn()}
        onNavigateTab={vi.fn()}
      />
    );

    expect(screen.getByText('无法获取 A2S 状态查询响应，请检查服务器网络路由、游戏端口开放状态及防火墙规则。')).toBeInTheDocument();
    expect(screen.queryByText(/UDP 27015/)).not.toBeInTheDocument();
  });
});
