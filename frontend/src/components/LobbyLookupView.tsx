import React, { useState } from 'react';
import { LobbyLookupResult } from '../types';
import { ApiService } from '../services/api';
import { Badge } from './common/Badge';
import { Tooltip } from './common/Tooltip';
import { formatDateTime } from '../utils/statusMapping';
import {
  Search,
  Loader2,
  AlertCircle,
  Clock,
  User,
  CheckCircle2,
  XCircle,
  HelpCircle,
  Database,
  Hash,
  Copy,
} from 'lucide-react';

export const LobbyLookupView: React.FC = () => {
  const [lobbyIdInput, setLobbyIdInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [result, setResult] = useState<LobbyLookupResult | null>(null);
  const [isStaleData, setIsStaleData] = useState(false);
  const [uriCopied, setUriCopied] = useState(false);

  const joinUri = result?.ownerSteamId
    ? `steam://joinlobby/550/${result.lobbyId}/${result.ownerSteamId}`
    : null;

  const copyJoinUri = async () => {
    if (!joinUri) return;
    await navigator.clipboard.writeText(joinUri);
    setUriCopied(true);
    window.setTimeout(() => setUriCopied(false), 1800);
  };

  // Validate non-zero decimal ID
  const isValidLobbyId = Boolean(
    lobbyIdInput.trim() &&
      /^\d+$/.test(lobbyIdInput.trim()) &&
      lobbyIdInput.trim() !== '0'
  );

  const handleSearch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!isValidLobbyId || isLoading) return;

    setIsLoading(true);
    setErrorMessage(null);

    try {
      const response = await ApiService.lookupLobby(lobbyIdInput.trim());
      if (response.data) {
        setResult(response.data);
        setIsStaleData(false);
        setUriCopied(false);
      } else {
        // Query failed: if we have existing cached result, keep it and mark as stale
        if (result) {
          setIsStaleData(true);
          setErrorMessage(`最新查询失败 (${response.error || '未响应'})，当前展示历史缓存数据。`);
        } else {
          setErrorMessage(response.error || '未查询到该大厅的信息或大厅已解散');
        }
      }
    } catch {
      if (result) {
        setIsStaleData(true);
        setErrorMessage('网络请求异常，当前展示历史缓存数据。');
      } else {
        setErrorMessage('连接控制服务失败，请稍后重试');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6 pb-12">
      {/* Page Title */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-slate-900">大厅查询</h1>
      </div>

      {/* Query Card */}
      <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-xs space-y-4">
        <form onSubmit={handleSearch} className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
          <div className="relative flex-1">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
              <Search className="w-4 h-4" />
            </div>
            <input
              type="text"
              value={lobbyIdInput}
              onChange={(e) => {
                // only allow numbers
                const val = e.target.value.replace(/\D/g, '');
                setLobbyIdInput(val);
              }}
              placeholder="请输入大厅 ID (64 位非零十进制数字)"
              className="w-full pl-9 pr-3 py-2 text-sm font-mono bg-white border border-slate-300 rounded-md focus:outline-hidden focus:ring-2 focus:ring-blue-500 text-slate-900 placeholder:text-slate-400"
              disabled={isLoading}
              autoFocus
            />
          </div>

          <button
            type="submit"
            disabled={!isValidLobbyId || isLoading}
            className="inline-flex items-center justify-center gap-2 px-5 py-2 text-xs font-medium text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-md shadow-xs focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shrink-0"
          >
            {isLoading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>正在查询</span>
              </>
            ) : (
              <>
                <Search className="w-4 h-4" />
                <span>查询大厅</span>
              </>
            )}
          </button>
        </form>

        {/* Error message */}
        {errorMessage && (
          <div className="p-3 bg-amber-50 border border-amber-200 rounded-md text-xs text-amber-900 flex items-start gap-2">
            <AlertCircle className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
            <div className="leading-relaxed">{errorMessage}</div>
          </div>
        )}
      </div>

      {/* Result Display Area */}
      {result && (
        <div className="space-y-5">
          {/* Stale Warning Banner */}
          {isStaleData && (
            <div className="p-3 bg-amber-100 border border-amber-300 rounded-lg text-xs text-amber-900 flex items-center gap-2 font-medium">
              <AlertCircle className="w-4 h-4 text-amber-700 shrink-0" />
              <span>结果可能已过期：本次查询未获取到控制服务最新实时响应，以下展示此前缓存的观测数据。</span>
            </div>
          )}

          {/* Overview Info Card */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs p-5 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <Hash className="w-5 h-5 text-blue-600" />
                <span className="text-base font-bold text-slate-900 font-mono">
                  {result.lobbyId}
                </span>
              </div>
              <div className="flex items-center gap-2">
                {result.memberStatus === 'complete' ? (
                  <Badge
                    label="成员数据完整"
                    badgeClass="bg-emerald-50 text-emerald-800 border-emerald-200"
                    dotClass="bg-emerald-600"
                  />
                ) : (
                  <Badge
                    label="成员数据未确认"
                    badgeClass="bg-amber-50 text-amber-800 border-amber-200"
                    dotClass="bg-amber-500"
                  />
                )}
              </div>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-xs">
              <div>
                <span className="text-slate-500 block mb-1">大厅所有者</span>
                <Tooltip content={`Steam 64 位 ID: ${result.ownerSteamId}`}>
                  <span className="font-mono font-medium text-slate-800 bg-slate-50 px-2 py-0.5 rounded border border-slate-200">
                    {result.ownerSteamId}
                  </span>
                </Tooltip>
              </div>

              <div>
                <span className="text-slate-500 block mb-1">已确认成员数</span>
                <span className="font-semibold text-slate-900 text-sm">
                  {result.confirmedMemberCount !== null ? (
                    `${result.confirmedMemberCount} 人`
                  ) : (
                    <span className="text-amber-700 text-xs font-normal">
                      成员数据未确认
                    </span>
                  )}
                </span>
              </div>

              <div>
                <span className="text-slate-500 block mb-1">观测时间</span>
                <div className="flex items-center gap-1 font-mono text-slate-700 text-[11px]">
                  <Clock className="w-3.5 h-3.5 text-slate-400" />
                  <span>{formatDateTime(result.observedAt)}</span>
                </div>
              </div>

              <div>
                <span className="text-slate-500 block mb-1">目标服务器匹配</span>
                <span className="font-mono text-slate-800 text-[11px] truncate block">
                  {result.metadata?.server_endpoint || '自动匹配分发'}
                </span>
              </div>
            </div>

            <div className="space-y-1.5">
              <span className="text-xs text-slate-500 block">一键加入大厅 URI</span>
              {joinUri ? (
                <div className="flex flex-col sm:flex-row gap-2">
                  <input
                    aria-label="Steam 大厅加入 URI"
                    className="min-w-0 flex-1 px-2.5 py-1.5 text-xs font-mono text-slate-700 bg-slate-50 border border-slate-200 rounded-md"
                    readOnly
                    value={joinUri}
                    onFocus={(event) => event.currentTarget.select()}
                  />
                  <button
                    type="button"
                    aria-label="复制加入 URI"
                    onClick={() => void copyJoinUri()}
                    className="inline-flex items-center justify-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 focus:outline-hidden focus:ring-2 focus:ring-blue-500 shrink-0"
                  >
                    <Copy className="w-3.5 h-3.5" />
                    <span>{uriCopied ? '已复制' : '复制加入 URI'}</span>
                  </button>
                </div>
              ) : (
                <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-md px-2.5 py-2">
                  当前结果未返回房主 Steam ID，暂时无法生成完整加入 URI。
                </p>
              )}
            </div>
          </div>

          {/* Members Table */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="px-5 py-3.5 border-b border-slate-200 bg-slate-50/60 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-900">大厅成员列表</h2>
              <span className="text-xs text-slate-500 font-mono">
                {result.members?.length || 0} 名已知成员
              </span>
            </div>

            {result.memberStatus === 'unconfirmed' && (!result.members || result.members.length === 0) ? (
              <div className="p-8 text-center text-xs text-amber-800 bg-amber-50/40">
                <HelpCircle className="w-6 h-6 text-amber-500 mx-auto mb-1.5" />
                <p className="font-medium">成员数据未确认</p>
                <p className="text-slate-500 text-[11px] mt-0.5">
                  当前大厅已创建或连接中，但 Steam API 尚未回传完整成员槽位，无法推断确切成员数量。
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="bg-slate-50/80 text-slate-600 border-b border-slate-200 font-medium">
                      <th className="py-2.5 px-4 font-semibold">成员昵称</th>
                      <th className="py-2.5 px-4 font-semibold">Steam 64 位 ID</th>
                      <th className="py-2.5 px-4 font-semibold">就绪状态</th>
                      <th className="py-2.5 px-4 font-semibold">网络延迟</th>
                      <th className="py-2.5 px-4 font-semibold">加入时间</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 text-slate-700">
                    {result.members?.map((member, idx) => (
                      <tr key={member.steamId || idx} className="hover:bg-slate-50/80">
                        <td className="py-2.5 px-4 font-medium text-slate-900 flex items-center gap-1.5">
                          {member.avatarUrl ? <img alt={`${member.personaName || member.steamId} 头像`} className="h-8 w-8 rounded-full object-cover" src={member.avatarUrl} /> : <User aria-label="未知玩家头像" className="h-8 w-8 rounded-full bg-slate-100 p-1.5 text-slate-400" />}
                          <span>{member.personaName || '未知玩家'}</span>
                          {member.steamId === result.ownerSteamId && (
                            <span className="text-[10px] font-bold text-blue-700 bg-blue-50 px-1 py-0.2 rounded border border-blue-200">
                              房主
                            </span>
                          )}
                        </td>
                        <td className="py-2.5 px-4 font-mono text-slate-600">
                          {member.steamId}
                        </td>
                        <td className="py-2.5 px-4">
                          {member.isReady ? (
                            <span className="inline-flex items-center gap-1 text-emerald-700 font-medium">
                              <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                              <span>已就绪</span>
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 text-slate-400">
                              <XCircle className="w-3.5 h-3.5 text-slate-400" />
                              <span>未就绪</span>
                            </span>
                          )}
                        </td>
                        <td className="py-2.5 px-4 font-mono text-slate-600">
                          {member.ping !== undefined ? `${member.ping} ms` : '--'}
                        </td>
                        <td className="py-2.5 px-4 font-mono text-[11px] text-slate-500">
                          {formatDateTime(member.joinedAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Session Metadata (会话元数据) with strict headers "键" and "值" */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-xs overflow-hidden">
            <div className="px-5 py-3.5 border-b border-slate-200 bg-slate-50/60 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Database className="w-4 h-4 text-slate-600" />
                <h2 className="text-sm font-semibold text-slate-900">会话元数据</h2>
              </div>
              <span className="text-xs text-slate-400">保留原始键值</span>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs border-collapse font-mono">
                <thead>
                  <tr className="bg-slate-50/80 text-slate-600 border-b border-slate-200">
                    <th className="py-2 px-4 w-1/3 font-semibold text-slate-700 font-sans">
                      键
                    </th>
                    <th className="py-2 px-4 w-2/3 font-semibold text-slate-700 font-sans">
                      值
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {Object.entries(result.metadata || {}).map(([rawKey, rawValue]) => (
                    <tr key={rawKey} className="hover:bg-slate-50/80">
                      <td className="py-2 px-4 text-slate-600 font-medium">
                        {rawKey}
                      </td>
                      <td className="py-2 px-4 text-slate-900 break-all">
                        {rawValue}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
