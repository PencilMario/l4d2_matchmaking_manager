import { useEffect, useState, type FormEvent } from 'react';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { Switch } from '../../components/common/Switch';
import type {
  GlobalSettings,
  SteamWebApiKeySettings,
  SteamWebApiKeySettingsInput,
  VncProxySettings,
  VncProxySettingsInput,
  WarmupSchedulingSettings,
  WarmupSchedulingSettingsInput,
} from '../../api/models';
import { describeError } from '../../state/display';

type Props = {
  getSettings: () => Promise<GlobalSettings>;
  updateProxy: (input: VncProxySettingsInput) => Promise<VncProxySettings>;
  updateKey: (input: SteamWebApiKeySettingsInput) => Promise<SteamWebApiKeySettings>;
  updateWarmupScheduling: (input: WarmupSchedulingSettingsInput) => Promise<WarmupSchedulingSettings>;
};

export function SettingsWorkspace({ getSettings, updateProxy, updateKey, updateWarmupScheduling }: Props) {
  const [settings, setSettings] = useState<GlobalSettings | null>(null);
  const [proxy, setProxy] = useState('');
  const [steamWebApiKey, setSteamWebApiKey] = useState('');
  const [warmupSchedulingEnabled, setWarmupSchedulingEnabled] = useState(true);
  const [confirmDisable, setConfirmDisable] = useState(false);
  const [busy, setBusy] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void getSettings()
      .then(value => {
        if (!active) return;
        setSettings(value);
        setProxy(value.steamProxyUrl ?? '');
        setWarmupSchedulingEnabled(value.warmupSchedulingEnabled);
      })
      .catch(() => {
        if (active) setError('读取全局设置失败，请重试。');
      })
      .finally(() => {
        if (active) setBusy(false);
      });
    return () => {
      active = false;
    };
  }, [getSettings]);

  const saveProxy = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await updateProxy({ proxyUrl: proxy.trim() || null });
      setSettings(current => current ? { ...current, steamProxyUrl: value.proxyUrl, updatedAt: value.updatedAt } : current);
      setProxy(value.proxyUrl ?? '');
      setMessage('VNC 代理已保存。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  const saveSteamWebApiKey = async (event: FormEvent) => {
    event.preventDefault();
    if (!steamWebApiKey.trim()) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await updateKey({ apiKey: steamWebApiKey.trim() });
      setSettings(current => current ? { ...current, steamWebApiKeyConfigured: value.configured, updatedAt: value.updatedAt } : current);
      setSteamWebApiKey('');
      setMessage('Steam Web API Key 已保存。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  const clearSteamWebApiKey = async () => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await updateKey({ clear: true });
      setSettings(current => current ? { ...current, steamWebApiKeyConfigured: value.configured, updatedAt: value.updatedAt } : current);
      setSteamWebApiKey('');
      setMessage('Steam Web API Key 已清除。');
    } catch (caught) {
      setError(describeError(caught).message);
    } finally {
      setSaving(false);
    }
  };

  const saveWarmupScheduling = async (enabled: boolean) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const value = await updateWarmupScheduling({ enabled });
      setSettings(current => current ? { ...current, warmupSchedulingEnabled: value.enabled, updatedAt: value.updatedAt } : current);
      setWarmupSchedulingEnabled(value.enabled);
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
    void saveWarmupScheduling(true);
  };

  if (busy) return <p className="empty-state">正在读取全局设置</p>;

  return <section className="editor-panel settings-panel">
    <div className="settings-panel__global-toggle">
      <Switch
        checked={warmupSchedulingEnabled}
        description="关闭后会停止并清空当前暖服任务，暖服节点容器保持运行；重新开启后从空任务状态恢复调度。"
        disabled={saving}
        id="workspace-global-warmup-scheduling"
        label="全局启用暖服和调度"
        onChange={toggleWarmupScheduling}
      />
    </div>
    <h2>Steam 设置</h2>
    <p>代理仅用于开启 VNC 服务的暖服节点。Steam Web API Key 仅由控制服务加密保存，用于补全大厅成员的公开资料。</p>
    <form className="form-grid" onSubmit={saveProxy}>
      <label className="form-field form-field--wide">VNC 代理地址
        <input aria-label="Steam 代理地址" onChange={event => setProxy(event.target.value)} placeholder="http://127.0.0.1:7890" value={proxy} />
        <small>支持 HTTP 或 HTTPS 代理，例如 http://127.0.0.1:7890。留空表示不配置。</small>
      </label>
      <div className="form-actions form-field--wide"><button className="button button--primary" disabled={saving} type="submit">{saving ? '正在保存' : '保存 VNC 代理'}</button></div>
    </form>
    <form className="form-grid" onSubmit={saveSteamWebApiKey}>
      <label className="form-field form-field--wide">Steam Web API Key
        <input aria-label="Steam Web API Key" onChange={event => setSteamWebApiKey(event.target.value)} placeholder={settings?.steamWebApiKeyConfigured ? '已配置，输入新 Key 以替换' : '输入 Steam Web API Key'} type="password" value={steamWebApiKey} />
        <small>当前状态：{settings?.steamWebApiKeyConfigured ? '已配置' : '未配置'}</small>
      </label>
      <div className="form-actions form-field--wide">
        <button className="button button--primary" disabled={saving || !steamWebApiKey.trim()} type="submit">{saving ? '正在保存' : '保存 Steam Web API Key'}</button>
        {settings?.steamWebApiKeyConfigured ? <button className="button button--quiet" disabled={saving} onClick={() => void clearSteamWebApiKey()} type="button">清除 Steam Web API Key</button> : null}
      </div>
    </form>
    {error && <p className="inline-error form-field--wide">{error}</p>}
    {message && <p className="success-message form-field--wide">{message}</p>}
    {settings && <small className="settings-panel__updated">上次更新：{new Date(settings.updatedAt).toLocaleString('zh-CN', { hour12: false })}</small>}
    <ConfirmDialog
      confirmLabel="确认禁用"
      description="控制服务会停止并清空所有当前暖服任务。暖服节点容器不会停止，之后可以重新启用调度。"
      intent="danger"
      isLoading={saving}
      isOpen={confirmDisable}
      onClose={() => setConfirmDisable(false)}
      onConfirm={() => { setConfirmDisable(false); void saveWarmupScheduling(false); }}
      title="确认禁用暖服和调度"
    />
  </section>;
}
