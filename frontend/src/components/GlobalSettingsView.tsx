import { useEffect, useState, type FormEvent } from 'react';
import type { WarmupPauseWindow, WarmupPauseWindowsSettings } from '../api/models';
import { ConfirmDialog } from './common/ConfirmDialog';
import { Switch } from './common/Switch';
import { WarmupPauseWindowsForm } from './settings/WarmupPauseWindowsForm';
import { describeError } from '../state/display';

type Settings = {
  steamProxyUrl: string | null;
  steamWebApiKeyConfigured: boolean;
  warmupSchedulingEnabled: boolean;
  warmupPauseWindows: WarmupPauseWindow[];
  warmupPauseWindowsActive: boolean;
  updatedAt: string;
};
type ProxySettings = { proxyUrl: string | null; updatedAt: string };
type KeySettings = { configured: boolean; updatedAt: string };
type WarmupSchedulingSettings = { enabled: boolean; updatedAt: string };

export function GlobalSettingsView({
  load,
  saveProxy,
  saveKey,
  saveWarmupScheduling,
  saveWarmupPauseWindows,
}: {
  load: () => Promise<Settings>;
  saveProxy: (input: { proxyUrl: string | null }) => Promise<ProxySettings>;
  saveKey: (input: { apiKey?: string; clear?: boolean }) => Promise<KeySettings>;
  saveWarmupScheduling: (input: { enabled: boolean }) => Promise<WarmupSchedulingSettings>;
  saveWarmupPauseWindows: (input: { windows: WarmupPauseWindow[] }) => Promise<WarmupPauseWindowsSettings>;
}) {
  const [proxy, setProxy] = useState('');
  const [steamWebApiKey, setSteamWebApiKey] = useState('');
  const [keyConfigured, setKeyConfigured] = useState(false);
  const [warmupSchedulingEnabled, setWarmupSchedulingEnabled] = useState(true);
  const [warmupPauseWindows, setWarmupPauseWindows] = useState<WarmupPauseWindow[]>([]);
  const [warmupPauseWindowsActive, setWarmupPauseWindowsActive] = useState(false);
  const [confirmDisable, setConfirmDisable] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [proxySaving, setProxySaving] = useState(false);
  const [keySaving, setKeySaving] = useState(false);
  const [schedulingSaving, setSchedulingSaving] = useState(false);
  const [proxyMessage, setProxyMessage] = useState<string | null>(null);
  const [proxyError, setProxyError] = useState<string | null>(null);
  const [keyMessage, setKeyMessage] = useState<string | null>(null);
  const [keyError, setKeyError] = useState<string | null>(null);
  const [schedulingMessage, setSchedulingMessage] = useState<string | null>(null);
  const [schedulingError, setSchedulingError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void load()
      .then(value => {
        if (!active) return;
        setProxy(value.steamProxyUrl ?? '');
        setKeyConfigured(value.steamWebApiKeyConfigured);
        setWarmupSchedulingEnabled(value.warmupSchedulingEnabled);
        setWarmupPauseWindows(value.warmupPauseWindows ?? []);
        setWarmupPauseWindowsActive(value.warmupPauseWindowsActive ?? false);
        setUpdatedAt(value.updatedAt);
      })
      .catch(() => {
        if (active) setProxyError('读取全局设置失败，请重试。');
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [load]);

  const submitProxy = async (event: FormEvent) => {
    event.preventDefault();
    setProxySaving(true);
    setProxyError(null);
    setProxyMessage(null);
    try {
      const value = await saveProxy({ proxyUrl: proxy.trim() || null });
      setProxy(value.proxyUrl ?? '');
      setUpdatedAt(value.updatedAt);
      setProxyMessage('VNC 代理已保存。');
    } catch (caught) {
      setProxyError(describeError(caught).message);
    } finally {
      setProxySaving(false);
    }
  };

  const submitKey = async (event: FormEvent) => {
    event.preventDefault();
    if (!steamWebApiKey.trim()) return;
    setKeySaving(true);
    setKeyError(null);
    setKeyMessage(null);
    try {
      const value = await saveKey({ apiKey: steamWebApiKey.trim() });
      setKeyConfigured(value.configured);
      setSteamWebApiKey('');
      setUpdatedAt(value.updatedAt);
      setKeyMessage('Steam Web API 密钥已保存。');
    } catch (caught) {
      setKeyError(describeError(caught).message);
    } finally {
      setKeySaving(false);
    }
  };

  const clearKey = async () => {
    setKeySaving(true);
    setKeyError(null);
    setKeyMessage(null);
    try {
      const value = await saveKey({ clear: true });
      setKeyConfigured(value.configured);
      setSteamWebApiKey('');
      setUpdatedAt(value.updatedAt);
      setKeyMessage('Steam Web API 密钥已清除。');
    } catch (caught) {
      setKeyError(describeError(caught).message);
    } finally {
      setKeySaving(false);
    }
  };

  const updateWarmupScheduling = async (enabled: boolean) => {
    setSchedulingSaving(true);
    setSchedulingError(null);
    setSchedulingMessage(null);
    try {
      const value = await saveWarmupScheduling({ enabled });
      setWarmupSchedulingEnabled(value.enabled);
      setUpdatedAt(value.updatedAt);
      setSchedulingMessage(value.enabled ? '暖服和调度已启用。' : '暖服和调度已禁用，当前任务已清空。');
    } catch (caught) {
      setSchedulingError(describeError(caught).message);
    } finally {
      setSchedulingSaving(false);
    }
  };

  const updatePauseWindows = async (input: { windows: WarmupPauseWindow[] }) => {
    const value = await saveWarmupPauseWindows(input);
    setWarmupPauseWindows(value.windows);
    setWarmupPauseWindowsActive(value.active);
    setUpdatedAt(value.updatedAt);
    return value;
  };

  const toggleWarmupScheduling = () => {
    if (warmupSchedulingEnabled) {
      setConfirmDisable(true);
      return;
    }
    void updateWarmupScheduling(true);
  };

  if (loading) return <section><h1 className="text-xl font-bold text-slate-900">全局设置</h1><p className="mt-4 text-sm text-slate-500">正在读取全局设置</p></section>;

  return <section className="space-y-4">
    <h1 className="text-xl font-bold text-slate-900">全局设置</h1>
    <div className="grid max-w-5xl gap-4 md:grid-cols-2">
      <article className="settings-card rounded-xl border border-slate-200 border-l-4 border-l-blue-600 bg-white p-5 shadow-sm" aria-labelledby="warmup-card-title">
        <div className="settings-card__header flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold tracking-[0.16em] text-blue-600">运行控制</p>
            <h2 className="mt-1 text-base font-semibold text-slate-900" id="warmup-card-title">暖服和调度</h2>
          </div>
          <span className={`rounded-full px-2 py-1 text-[10px] font-semibold ${warmupSchedulingEnabled ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>{warmupSchedulingEnabled ? '已启用' : '已禁用'}</span>
        </div>
        <p className="mt-3 text-xs leading-5 text-slate-500">手动控制全局暖服与调度。关闭会停止并清空当前任务，但暖服节点容器保持运行。</p>
        <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3">
          <Switch
            checked={warmupSchedulingEnabled}
            description="重新开启后从空任务状态恢复调度。"
            disabled={schedulingSaving}
            id="global-warmup-scheduling"
            label="全局启用暖服和调度"
            onChange={toggleWarmupScheduling}
          />
        </div>
        {schedulingError && <p className="mt-3 text-xs text-red-700" role="alert">{schedulingError}</p>}
        {schedulingMessage && <p className="mt-3 text-xs text-emerald-700" role="status">{schedulingMessage}</p>}
        {updatedAt && <small className="mt-4 block text-[11px] text-slate-400">上次更新：{formatUpdatedAt(updatedAt)}</small>}
      </article>

      <article className="settings-card rounded-xl border border-slate-200 border-l-4 border-l-amber-500 bg-white p-5 shadow-sm" aria-labelledby="pause-card-title">
        <div className="settings-card__header flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold tracking-[0.16em] text-amber-600">时间策略</p>
            <h2 className="mt-1 text-base font-semibold text-slate-900" id="pause-card-title">暖服暂停时间段</h2>
          </div>
          <span className="rounded-full bg-amber-50 px-2 py-1 text-[10px] font-semibold text-amber-700">分钟级</span>
        </div>
        <WarmupPauseWindowsForm initialActive={warmupPauseWindowsActive} initialWindows={warmupPauseWindows} onSave={updatePauseWindows} />
      </article>

      <article className="settings-card rounded-xl border border-slate-200 border-l-4 border-l-cyan-500 bg-white p-5 shadow-sm" aria-labelledby="proxy-card-title">
        <div className="settings-card__header flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold tracking-[0.16em] text-cyan-600">网络连接</p>
            <h2 className="mt-1 text-base font-semibold text-slate-900" id="proxy-card-title">VNC 代理</h2>
          </div>
          <span className="rounded-full bg-cyan-50 px-2 py-1 text-[10px] font-semibold text-cyan-700">可选</span>
        </div>
        <p className="mt-3 text-xs leading-5 text-slate-500">仅用于开启 VNC 服务的暖服节点。留空表示不配置。</p>
        <form className="mt-4 space-y-3" onSubmit={submitProxy}>
          <label className="block text-xs font-medium text-slate-700">VNC 代理地址
            <input aria-label="VNC 代理地址" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-xs" onChange={event => setProxy(event.target.value)} placeholder="http://127.0.0.1:7890" value={proxy} />
          </label>
          <button className="rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white disabled:opacity-50" disabled={proxySaving} type="submit">{proxySaving ? '正在保存' : '保存 VNC 代理'}</button>
        </form>
        {proxyError && <p className="mt-3 text-xs text-red-700" role="alert">{proxyError}</p>}
        {proxyMessage && <p className="mt-3 text-xs text-emerald-700" role="status">{proxyMessage}</p>}
        {updatedAt && <small className="mt-4 block text-[11px] text-slate-400">上次更新：{formatUpdatedAt(updatedAt)}</small>}
      </article>

      <article className="settings-card rounded-xl border border-slate-200 border-l-4 border-l-violet-500 bg-white p-5 shadow-sm" aria-labelledby="key-card-title">
        <div className="settings-card__header flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold tracking-[0.16em] text-violet-600">Steam 数据对接</p>
            <h2 className="mt-1 text-base font-semibold text-slate-900" id="key-card-title">Steam Web API 密钥</h2>
          </div>
          <span className={`rounded-full px-2 py-1 text-[10px] font-semibold ${keyConfigured ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>{keyConfigured ? '已配置' : '未配置'}</span>
        </div>
        <p className="mt-3 text-xs leading-5 text-slate-500">控制服务会加密保存此密钥，用于补全大厅成员的公开资料。密钥不会回显。</p>
        <form className="mt-4 space-y-3" onSubmit={submitKey}>
          <label className="block text-xs font-medium text-slate-700">Steam Web API 密钥
            <input aria-label="Steam Web API 密钥" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-xs" onChange={event => setSteamWebApiKey(event.target.value)} placeholder={keyConfigured ? '已配置，输入新密钥以替换' : '输入 Steam Web API 密钥'} type="password" value={steamWebApiKey} />
            <span className="mt-1 block text-[11px] text-slate-500">当前状态：{keyConfigured ? '已配置' : '未配置'}</span>
          </label>
          <div className="flex flex-wrap gap-2">
            <button className="rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white disabled:opacity-50" disabled={keySaving || !steamWebApiKey.trim()} type="submit">{keySaving ? '正在保存' : '保存 Steam Web API 密钥'}</button>
            {keyConfigured && <button className="rounded-md border border-slate-300 px-3 py-2 text-xs font-medium text-slate-700 disabled:opacity-50" disabled={keySaving} onClick={() => void clearKey()} type="button">清除 Steam Web API 密钥</button>}
          </div>
        </form>
        {keyError && <p className="mt-3 text-xs text-red-700" role="alert">{keyError}</p>}
        {keyMessage && <p className="mt-3 text-xs text-emerald-700" role="status">{keyMessage}</p>}
        {updatedAt && <small className="mt-4 block text-[11px] text-slate-400">上次更新：{formatUpdatedAt(updatedAt)}</small>}
      </article>
    </div>
    <ConfirmDialog
      confirmLabel="确认禁用"
      description="控制服务会停止并清空所有当前暖服任务。暖服节点容器不会停止，之后可以重新启用调度。"
      intent="danger"
      isLoading={schedulingSaving}
      isOpen={confirmDisable}
      onClose={() => setConfirmDisable(false)}
      onConfirm={() => { setConfirmDisable(false); void updateWarmupScheduling(false); }}
      title="确认禁用暖服和调度"
    />
  </section>;
}

function formatUpdatedAt(value: string) {
  return new Date(value).toLocaleString('zh-CN', { hour12: false });
}
