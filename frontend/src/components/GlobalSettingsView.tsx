import { useEffect, useState, type FormEvent } from 'react';
import { ConfirmDialog } from './common/ConfirmDialog';
import { Switch } from './common/Switch';
import { describeError } from '../state/display';

type Settings = { steamProxyUrl: string | null; steamWebApiKeyConfigured: boolean; warmupSchedulingEnabled: boolean; updatedAt: string };
type ProxySettings = { proxyUrl: string | null; updatedAt: string };
type KeySettings = { configured: boolean; updatedAt: string };
type WarmupSchedulingSettings = { enabled: boolean; updatedAt: string };

export function GlobalSettingsView({
  load,
  saveProxy,
  saveKey,
  saveWarmupScheduling,
}: {
  load: () => Promise<Settings>;
  saveProxy: (input: { proxyUrl: string | null }) => Promise<ProxySettings>;
  saveKey: (input: { apiKey?: string; clear?: boolean }) => Promise<KeySettings>;
  saveWarmupScheduling: (input: { enabled: boolean }) => Promise<WarmupSchedulingSettings>;
}) {
  const [proxy, setProxy] = useState('');
  const [steamWebApiKey, setSteamWebApiKey] = useState('');
  const [keyConfigured, setKeyConfigured] = useState(false);
  const [warmupSchedulingEnabled, setWarmupSchedulingEnabled] = useState(true);
  const [confirmDisable, setConfirmDisable] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void load()
      .then(value => {
        if (!active) return;
        setProxy(value.steamProxyUrl ?? '');
        setKeyConfigured(value.steamWebApiKeyConfigured);
        setWarmupSchedulingEnabled(value.warmupSchedulingEnabled);
        setUpdatedAt(value.updatedAt);
      })
      .catch(() => {
        if (active) setError('读取全局设置失败，请重试。');
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
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await saveProxy({ proxyUrl: proxy.trim() || null });
      setProxy(value.proxyUrl ?? '');
      setUpdatedAt(value.updatedAt);
      setMessage('VNC 代理已保存。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  const submitKey = async (event: FormEvent) => {
    event.preventDefault();
    if (!steamWebApiKey.trim()) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await saveKey({ apiKey: steamWebApiKey.trim() });
      setKeyConfigured(value.configured);
      setSteamWebApiKey('');
      setUpdatedAt(value.updatedAt);
      setMessage('Steam Web API Key 已保存。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  const clearKey = async () => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await saveKey({ clear: true });
      setKeyConfigured(value.configured);
      setSteamWebApiKey('');
      setUpdatedAt(value.updatedAt);
      setMessage('Steam Web API Key 已清除。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  const updateWarmupScheduling = async (enabled: boolean) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await saveWarmupScheduling({ enabled });
      setWarmupSchedulingEnabled(value.enabled);
      setUpdatedAt(value.updatedAt);
      setMessage(value.enabled ? '暖服和调度已启用。' : '暖服和调度已禁用，当前任务已清空。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
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
    <div className="max-w-2xl rounded-lg border border-slate-200 bg-white p-5 shadow-xs space-y-4">
      <div className="rounded-md border border-amber-200 bg-amber-50 p-3">
        <Switch
          checked={warmupSchedulingEnabled}
          description="关闭后会停止并清空当前暖服任务，暖服节点容器保持运行；重新开启后从空任务状态恢复调度。"
          disabled={saving}
          id="global-warmup-scheduling"
          label="全局启用暖服和调度"
          onChange={toggleWarmupScheduling}
        />
      </div>
      <h2 className="text-sm font-semibold text-slate-900">Steam 设置</h2>
      <p className="mt-1 text-xs leading-5 text-slate-500">VNC 代理和 Steam Web API Key 分别保存；API Key 仅由控制服务加密保存，用于补全大厅成员的公开资料。</p>
      <form className="space-y-3" onSubmit={submitProxy}>
        <label className="block text-xs font-medium text-slate-700">VNC 代理地址
          <input aria-label="Steam 代理地址" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-xs" onChange={event => setProxy(event.target.value)} placeholder="http://127.0.0.1:7890" value={proxy} />
          <span className="mt-1 block text-[11px] text-slate-500">支持 HTTP 或 HTTPS 代理。留空表示不配置。</span>
        </label>
        <button className="rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white disabled:opacity-50" disabled={saving} type="submit">{saving ? '正在保存' : '保存 VNC 代理'}</button>
      </form>
      <form className="space-y-3" onSubmit={submitKey}>
        <label className="block text-xs font-medium text-slate-700">Steam Web API Key
          <input aria-label="Steam Web API Key" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-xs" onChange={event => setSteamWebApiKey(event.target.value)} placeholder={keyConfigured ? '已配置，输入新 Key 以替换' : '输入 Steam Web API Key'} type="password" value={steamWebApiKey} />
          <span className="mt-1 block text-[11px] text-slate-500">当前状态：{keyConfigured ? '已配置' : '未配置'}</span>
        </label>
        <div className="flex gap-2">
          <button className="rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white disabled:opacity-50" disabled={saving || !steamWebApiKey.trim()} type="submit">{saving ? '正在保存' : '保存 Steam Web API Key'}</button>
          {keyConfigured && <button className="rounded-md border border-slate-300 px-3 py-2 text-xs font-medium text-slate-700 disabled:opacity-50" disabled={saving} onClick={() => void clearKey()} type="button">清除 Steam Web API Key</button>}
        </div>
      </form>
      {error && <p className="text-xs text-red-700">{error}</p>}
      {message && <p className="text-xs text-emerald-700">{message}</p>}
      {updatedAt && <p className="mt-4 text-[11px] text-slate-400">上次更新：{new Date(updatedAt).toLocaleString('zh-CN', { hour12: false })}</p>}
    </div>
    <ConfirmDialog
      confirmLabel="确认禁用"
      description="控制服务会停止并清空所有当前暖服任务。暖服节点容器不会停止，之后可以重新启用调度。"
      intent="danger"
      isLoading={saving}
      isOpen={confirmDisable}
      onClose={() => setConfirmDisable(false)}
      onConfirm={() => { setConfirmDisable(false); void updateWarmupScheduling(false); }}
      title="确认禁用暖服和调度"
    />
  </section>;
}
