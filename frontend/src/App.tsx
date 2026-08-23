/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { useState, useEffect, useCallback, useRef } from 'react';
import {
  AppStateData,
  TabKey,
  TargetServer,
  WarmupAgent,
  SteamDownloadRegion,
} from './types';
import {
  ApiService,
  getAuthToken,
  setAuthToken,
  setUnauthorizedHandler,
} from './services/api';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { LoginView } from './components/LoginView';
import { LoadingView } from './components/LoadingView';
import { OverviewView } from './components/OverviewView';
import { TargetServersView } from './components/TargetServersView';
import { WarmupAgentsView } from './components/WarmupAgentsView';
import { LobbyLookupView } from './components/LobbyLookupView';
import { PlayerEntryStatisticsView } from './components/PlayerEntryStatisticsView';
import { GlobalSettingsView } from './components/GlobalSettingsView';
import { loadSteamDownloadRegions } from './services/steam-download-regions';

export default function App() {
  const [token, setToken] = useState<string>(() => getAuthToken());
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(Boolean(token));
  const [authError, setAuthError] = useState<string | null>(null);
  const [steamRegions, setSteamRegions] = useState<SteamDownloadRegion[]>([]);
  const [steamRegionsLoading, setSteamRegionsLoading] = useState(false);
  const [steamRegionsError, setSteamRegionsError] = useState<string | null>(null);

  // App State Data
  const [state, setState] = useState<AppStateData>({
    targets: [],
    agents: [],
    attempts: [],
    observations: [],
    lastUpdated: null,
    controllerHealthy: true,
    initialLoaded: false,
    isRefreshing: false,
  });

  // Active navigation tab
  const [activeTab, setActiveTab] = useState<TabKey>('overview');

  // Pause background polling while a settings modal is being edited.
  const [isPollingPaused, setIsPollingPaused] = useState(false);

  // Deep linking target selection
  const [selectedTargetId, setSelectedTargetId] = useState<string | null>(null);

  // Polling interval ref
  const pollingTimerRef = useRef<NodeJS.Timeout | null>(null);

  // Handle 401 Unauthorized
  const handleUnauthorized = useCallback(() => {
    setAuthToken('');
    setToken('');
    setIsAuthenticated(false);
    setAuthError('登录会话已失效，请重新输入访问令牌。');
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(handleUnauthorized);
  }, [handleUnauthorized]);

  useEffect(() => {
    if (!isAuthenticated || !token) {
      setSteamRegionsLoading(false);
      return;
    }

    let cancelled = false;
    setSteamRegionsLoading(true);
    setSteamRegionsError(null);
    void loadSteamDownloadRegions(() => ApiService.fetchSteamDownloadRegions())
      .then((regions) => {
        if (!cancelled) setSteamRegions(regions);
      })
      .catch((error: unknown) => {
        if (!cancelled) setSteamRegionsError(error instanceof Error ? error.message : '读取下载区域失败');
      })
      .finally(() => {
        if (!cancelled) setSteamRegionsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isAuthenticated, token]);

  // Synchronize state fetcher (used by both initial load, 5s polling, and manual refresh)
  const refreshData = useCallback(async (isManual = false) => {
    if (!token) return;

    if (isManual) {
      setState((prev) => ({ ...prev, isRefreshing: true }));
    }

    try {
      const data = await ApiService.fetchAllState();
      setState((prev) => ({
        ...prev,
        targets: data.targets,
        agents: data.agents,
        attempts: data.attempts,
        observations: data.observations,
        controllerHealthy: data.healthy,
        lastUpdated: new Date(),
        initialLoaded: true,
        isRefreshing: false,
      }));
    } catch {
      setState((prev) => ({
        ...prev,
        controllerHealthy: false,
        isRefreshing: false,
        initialLoaded: true,
      }));
    }
  }, [token]);

  // Setup 5-second polling loop
  useEffect(() => {
    if (!isAuthenticated || !token || isPollingPaused) {
      if (pollingTimerRef.current) clearInterval(pollingTimerRef.current);
      pollingTimerRef.current = null;
      return;
    }

    // Initial fetch
    refreshData();

    // 5-second recurring poll
    pollingTimerRef.current = setInterval(() => {
      refreshData();
    }, 5000);

    return () => {
      if (pollingTimerRef.current) clearInterval(pollingTimerRef.current);
    };
  }, [isAuthenticated, token, isPollingPaused, refreshData]);

  // Login handler
  const handleLogin = async (inputToken: string): Promise<boolean> => {
    setAuthToken(inputToken);
    setToken(inputToken);
    setAuthError(null);

    try {
      const data = await ApiService.fetchAllState();
      if (data.healthy) {
        setIsAuthenticated(true);
        setState((prev) => ({
          ...prev,
          targets: data.targets,
          agents: data.agents,
          attempts: data.attempts,
          observations: data.observations,
          controllerHealthy: true,
          lastUpdated: new Date(),
          initialLoaded: true,
        }));
        return true;
      } else {
        setAuthToken('');
        setToken('');
        setIsAuthenticated(false);
        return false;
      }
    } catch {
      setAuthToken('');
      setToken('');
      setIsAuthenticated(false);
      return false;
    }
  };

  // Logout handler
  const handleLogout = () => {
    setAuthToken('');
    setToken('');
    setIsAuthenticated(false);
    setState({
      targets: [],
      agents: [],
      attempts: [],
      observations: [],
      lastUpdated: null,
      controllerHealthy: true,
      initialLoaded: false,
      isRefreshing: false,
    });
  };

  // Navigation helpers
  const handleNavigateToTarget = (targetId: string) => {
    setSelectedTargetId(targetId);
    setActiveTab('targets');
  };

  // Target Server Operations
  const handleCreateTarget = async (data: any): Promise<boolean> => {
    const res = await ApiService.createTarget(data);
    if (res.data) {
      await refreshData();
      return true;
    }
    throw new Error(res.error || '创建目标服务器失败');
  };

  const handleUpdateTarget = async (id: string, data: any): Promise<boolean> => {
    const res = await ApiService.updateTarget(id, data);
    if (res.data) {
      await refreshData();
      return true;
    }
    throw new Error(res.error || '更新目标服务器失败');
  };

  const handleDeleteTarget = async (id: string): Promise<{ success: boolean; error?: string }> => {
    const res = await ApiService.deleteTarget(id);
    if (res.data?.success) {
      await refreshData();
      return { success: true };
    }
    await refreshData();
    return { success: false, error: res.error || '删除失败' };
  };

  const handleToggleTargetEnabled = async (
    id: string,
    enabled: boolean
  ): Promise<{ success: boolean; error?: string }> => {
    const res = await ApiService.toggleTargetEnabled(id, enabled);
    if (res.data) {
      await refreshData();
      return { success: true };
    }
    await refreshData();
    return { success: false, error: res.error || '切换调度状态失败' };
  };

  // Agent Operations
  const handleCreateAgent = async (data: { name: string; steamRegion?: string; keepVncAlive: boolean }): Promise<boolean> => {
    const res = await ApiService.createAgent(data);
    if (res.data) {
      await refreshData();
      return true;
    }
    throw new Error(res.error || '创建暖服节点失败');
  };

  const handleUpdateAgent = async (id: string, data: { name: string; steamRegion?: string; keepVncAlive: boolean }): Promise<boolean> => {
    const res = await ApiService.updateAgent(id, data);
    if (res.data) {
      await refreshData();
      return true;
    }
    throw new Error(res.error || '更新暖服节点失败');
  };

  const handleStartAgent = async (id: string): Promise<boolean> => {
    const res = await ApiService.startAgent(id);
    if (res.data) {
      await refreshData();
      return true;
    }
    return false;
  };

  const handleStopAgent = async (id: string): Promise<boolean> => {
    const res = await ApiService.stopAgent(id);
    if (res.data) {
      await refreshData();
      return true;
    }
    return false;
  };

  const handleRebuildAgent = async (id: string): Promise<{ success: boolean; error?: string }> => {
    const res = await ApiService.rebuildAgent(id);
    if (res.data) {
      await refreshData();
      return { success: true };
    }
    return { success: false, error: res.error || '重建失败' };
  };

  const handleOpenAgentVncSession = async (id: string): Promise<{ url: string; expiresAt: string }> => {
    const res = await ApiService.openAgentVncSession(id);
    if (res.data) return res.data;
    throw new Error(res.error || '打开 VNC 连接失败');
  };

  const handleDeleteAgent = async (id: string): Promise<boolean> => {
    const res = await ApiService.deleteAgent(id);
    if (res.data?.success) {
      await refreshData();
      return true;
    }
    return false;
  };

  // Unauthenticated view
  if (!isAuthenticated) {
    return <LoginView onLogin={handleLogin} initialError={authError} />;
  }

  // Initial loading screen before first data load finishes
  if (!state.initialLoaded) {
    return <LoadingView />;
  }

  // Summary counts for navigation badge indicators
  const uncertainCount = state.attempts.filter((a) => a.status === 'uncertain').length;
  const unavailableCount = state.targets.filter((t) => t.a2sStatus === 'unavailable').length;
  const quarantinedCount = state.agents.filter((a) => a.status === 'quarantined').length;

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-[#f4f5f7] text-slate-800 font-sans antialiased">
      {/* Fixed Left Sidebar */}
      <Sidebar
        activeTab={activeTab}
        onTabChange={(tab) => {
          setActiveTab(tab);
          if (tab !== 'targets') {
            setSelectedTargetId(null);
          }
        }}
        uncertainCount={uncertainCount}
        unavailableCount={unavailableCount}
        quarantinedCount={quarantinedCount}
      />

      {/* Main Container */}
      <div className="flex-1 flex flex-col min-w-0 h-full overflow-hidden">
        {/* Top Header */}
        <Header
          controllerHealthy={state.controllerHealthy}
          lastUpdated={state.lastUpdated}
          isRefreshing={state.isRefreshing}
          onRefresh={() => refreshData(true)}
          onLogout={handleLogout}
        />

        {/* Main Content Area */}
        <main className="flex-1 overflow-y-auto px-6 py-5">
          <div className="max-w-7xl mx-auto">
            {activeTab === 'overview' && (
              <OverviewView
                targets={state.targets}
                agents={state.agents}
                attempts={state.attempts}
                observations={state.observations}
                steamRegions={steamRegions}
                onNavigateToTarget={handleNavigateToTarget}
                onNavigateTab={(tab) => setActiveTab(tab)}
              />
            )}

            {activeTab === 'targets' && (
              <TargetServersView
                targets={state.targets}
                attempts={state.attempts}
                onRefresh={() => refreshData(true)}
                onCreateTarget={handleCreateTarget}
                onUpdateTarget={handleUpdateTarget}
                onDeleteTarget={handleDeleteTarget}
                onToggleTargetEnabled={handleToggleTargetEnabled}
                onModalOpenChange={setIsPollingPaused}
                selectedServerId={selectedTargetId}
                onClearSelectedServer={() => setSelectedTargetId(null)}
                isRefreshing={state.isRefreshing}
              />
            )}

            {activeTab === 'agents' && (
              <WarmupAgentsView
                agents={state.agents}
                attempts={state.attempts}
                onRefresh={() => refreshData(true)}
                onCreateAgent={handleCreateAgent}
                onUpdateAgent={handleUpdateAgent}
                onStartAgent={handleStartAgent}
                onStopAgent={handleStopAgent}
                onRebuildAgent={handleRebuildAgent}
                onOpenVncSession={handleOpenAgentVncSession}
                onDeleteAgent={handleDeleteAgent}
                onModalOpenChange={setIsPollingPaused}
                isRefreshing={state.isRefreshing}
                steamRegions={steamRegions}
                isSteamRegionsLoading={steamRegionsLoading}
                steamRegionsError={steamRegionsError}
              />
            )}

            {activeTab === 'lobby' && <LobbyLookupView />}
            {activeTab === 'statistics' && <PlayerEntryStatisticsView agents={state.agents} targets={state.targets} />}
            {activeTab === 'settings' && <GlobalSettingsView load={async () => { const result = await ApiService.getGlobalSettings(); if (!result.data) throw new Error(result.error || '读取全局设置失败'); return result.data; }} saveProxy={async input => { const result = await ApiService.updateVncProxySettings(input); if (!result.data) throw new Error(result.error || '保存 VNC 代理失败'); return result.data; }} saveKey={async input => { const result = await ApiService.updateSteamWebApiKeySettings(input); if (!result.data) throw new Error(result.error || '保存 Steam Web API Key 失败'); return result.data; }} saveWarmupScheduling={async input => { const result = await ApiService.updateWarmupSchedulingSettings(input); if (!result.data) throw new Error(result.error || '保存暖服和调度状态失败'); await refreshData(); return result.data; }} saveWarmupPauseWindows={async input => { const result = await ApiService.updateWarmupPauseWindowsSettings(input); if (!result.data) throw new Error(result.error || '保存暖服暂停时间段失败'); await refreshData(); return result.data; }} />}
          </div>
        </main>
      </div>
    </div>
  );
}
