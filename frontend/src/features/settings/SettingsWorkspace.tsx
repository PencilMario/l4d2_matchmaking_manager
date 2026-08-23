import { useEffect, useState, type FormEvent } from 'react';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { Switch } from '../../components/common/Switch';
import { WarmupPauseWindowsForm } from '../../components/settings/WarmupPauseWindowsForm';
import type {
  GlobalSettings,
  SteamWebApiKeySettings,
  SteamWebApiKeySettingsInput,
  VncProxySettings,
  VncProxySettingsInput,
  WarmupPauseWindow,
  WarmupPauseWindowsSettings,
  WarmupPauseWindowsSettingsInput,
  WarmupSchedulingSettings,
  WarmupSchedulingSettingsInput,
} from '../../api/models';
import { describeError } from '../../state/display';

type Props = {
  getSettings: () => Promise<GlobalSettings>;
  updateProxy: (input: VncProxySettingsInput) => Promise<VncProxySettings>;
  updateKey: (input: SteamWebApiKeySettingsInput) => Promise<SteamWebApiKeySettings>;
  updateWarmupScheduling: (input: WarmupSchedulingSettingsInput) => Promise<WarmupSchedulingSettings>;
  updateWarmupPauseWindows: (input: WarmupPauseWindowsSettingsInput) => Promise<WarmupPauseWindowsSettings>;
};

export function SettingsWorkspace({ getSettings, updateProxy, updateKey, updateWarmupScheduling, updateWarmupPauseWindows }: Props) {
  const [settings, setSettings] = useState<GlobalSettings | null>(null);
  const [proxy, setProxy] = useState('');
  const [steamWebApiKey, setSteamWebApiKey] = useState('');
  const [warmupSchedulingEnabled, setWarmupSchedulingEnabled] = useState(true);
  const [confirmDisable, setConfirmDisable] = useState(false);
  const [busy, setBusy] = useState(true);
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
    void getSettings()
      .then(value => {
        if (!active) return;
        setSettings(value);
        setProxy(value.steamProxyUrl ?? '');
        setWarmupSchedulingEnabled(value.warmupSchedulingEnabled);
      })
      .catch(() => {
        if (active) setSchedulingError('读取全局设置失败，请重试。');
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
    setProxySaving(true);
    setProxyError(null);
    setProxyMessage(null);
    try {
      const value = await updateProxy({ proxyUrl: proxy.trim() || null });
      setSettings(current => current ? { ...current, steamProxyUrl: value.proxyUrl, updatedAt: value.updatedAt } : current);
      setProxy(value.proxyUrl ?? '');
      setProxyMessage('VNC 代理已保存。');
    } catch (caught) {
      setProxyError(describeError(caught).message);
    } finally {
      setProxySaving(false);
    }
  };

  const saveSteamWebApiKey = async (event: FormEvent) => {
    event.preventDefault();
    if (!steamWebApiKey.trim()) return;
    setKeySaving(true);
    setKeyError(null);
    setKeyMessage(null);
    try {
      const value = await updateKey({ apiKey: steamWebApiKey.trim() });
      setSettings(current => current ? { ...current, steamWebApiKeyConfigured: value.configured, updatedAt: value.updatedAt } : current);
      setSteamWebApiKey('');
      setKeyMessage('Steam Web API 密钥已保存。');
    } catch (caught) {
      setKeyError(describeError(caught).message);
    } finally {
      setKeySaving(false);
    }
  };

  const clearSteamWebApiKey = async () => {
    setKeySaving(true);
    setKeyError(null);
    setKeyMessage(null);
    try {
      const value = await updateKey({ clear: true });
      setSettings(current => current ? { ...current, steamWebApiKeyConfigured: value.configured, updatedAt: value.updatedAt } : current);
      setSteamWebApiKey('');
      setKeyMessage('Steam Web API 密钥已清除。');
    } catch (caught) {
      setKeyError(describeError(caught).message);
    } finally {
      setKeySaving(false);
    }
  };

  const saveWarmupScheduling = async (enabled: boolean) => {
    setSchedulingSaving(true);
    setSchedulingError(null);
    setSchedulingMessage(null);
    try {
      const value = await updateWarmupScheduling({ enabled });
      setSettings(current => current ? { ...current, warmupSchedulingEnabled: value.enabled, updatedAt: value.updatedAt } : current);
      setWarmupSchedulingEnabled(value.enabled);
      setSchedulingMessage(value.enabled ? '暖服和调度已启用。' : '暖服和调度已禁用，当前任务已清空。');
    } catch (caught) {
      setSchedulingError(describeError(caught).message);
    } finally {
      setSchedulingSaving(false);
    }
  };

  const savePauseWindows = async (input: WarmupPauseWindowsSettingsInput) => {
    const value = await updateWarmupPauseWindows(input);
    setSettings(current => current ? { ...current, warmupPauseWindows: value.windows, warmupPauseWindowsActive: value.active, updatedAt: value.updatedAt } : current);
    return value;
  };

  const toggleWarmupScheduling = () => {
    if (warmupSchedulingEnabled) {
      setConfirmDisable(true);
      return;
    }
    void saveWarmupScheduling(true);
  };

  if (busy) return <p className="empty-state">正在读取全局设置</p>;

  return <section className="settings-panel">
    <div className="settings-panel__cards">
      <article className="settings-card" aria-labelledby="workspace-warmup-card-title">
        <div className="settings-card__header">
          <div>
            <p className="settings-card__eyebrow">运行控制</p>
            <h2 id="workspace-warmup-card-title">暖服和调度</h2>
          </div>
          <span className={`settings-card__badge ${warmupSchedulingEnabled ? 'settings-card__badge--success' : 'settings-card__badge--muted'}`}>{warmupSchedulingEnabled ? '已启用' : '已禁用'}</span>
        </div>
        <p className="settings-card__description">手动控制全局暖服与调度。关闭会停止并清空当前任务，但暖服节点容器保持运行。</p>
        <div className="settings-panel__global-toggle">
          <Switch
            checked={warmupSchedulingEnabled}
            description="重新开启后从空任务状态恢复调度。"
            disabled={schedulingSaving}
            id="workspace-global-warmup-scheduling"
            label="全局启用暖服和调度"
            onChange={toggleWarmupScheduling}
          />
        </div>
        {schedulingError && <p className="inline-error" role="alert">{schedulingError}</p>}
        {schedulingMessage && <p className="success-message" role="status">{schedulingMessage}</p>}
        {settings && <small className="settings-card__updated">上次更新：{formatUpdatedAt(settings.updatedAt)}</small>}
      </article>

      <article className="settings-card" aria-labelledby="workspace-pause-card-title">
        <div className="settings-card__header">
          <div>
            <p className="settings-card__eyebrow settings-card__eyebrow--amber">时间策略</p>
            <h2 id="workspace-pause-card-title">暖服暂停时间段</h2>
          </div>
          <span className="settings-card__badge settings-card__badge--amber">分钟级</span>
        </div>
        <WarmupPauseWindowsForm initialActive={settings?.warmupPauseWindowsActive ?? false} initialWindows={settings?.warmupPauseWindows ?? []} onSave={savePauseWindows} />
      </article>

      <article className="settings-card" aria-labelledby="workspace-proxy-card-title">
        <div className="settings-card__header">
          <div>
            <p className="settings-card__eyebrow settings-card__eyebrow--cyan">网络连接</p>
            <h2 id="workspace-proxy-card-title">VNC 代理</h2>
          </div>
          <span className="settings-card__badge settings-card__badge--cyan">可选</span>
        </div>
        <p className="settings-card__description">仅用于开启 VNC 服务的暖服节点。留空表示不配置。</p>
        <form className="form-grid settings-card__form" onSubmit={saveProxy}>
          <label className="form-field form-field--wide">VNC 代理地址
            <input aria-label="VNC 代理地址" onChange={event => setProxy(event.target.value)} placeholder="http://127.0.0.1:7890" value={proxy} />
            <small>支持 HTTP 或 HTTPS 代理，例如 http://127.0.0.1:7890。</small>
          </label>
          <div className="form-actions form-field--wide"><button className="button button--primary" disabled={proxySaving} type="submit">{proxySaving ? '正在保存' : '保存 VNC 代理'}</button></div>
        </form>
        {proxyError && <p className="inline-error" role="alert">{proxyError}</p>}
        {proxyMessage && <p className="success-message" role="status">{proxyMessage}</p>}
        {settings && <small className="settings-card__updated">上次更新：{formatUpdatedAt(settings.updatedAt)}</small>}
      </article>

      <article className="settings-card" aria-labelledby="workspace-key-card-title">
        <div className="settings-card__header">
          <div>
            <p className="settings-card__eyebrow settings-card__eyebrow--violet">Steam 数据对接</p>
            <h2 id="workspace-key-card-title">Steam Web API 密钥</h2>
          </div>
          <span className={`settings-card__badge ${settings?.steamWebApiKeyConfigured ? 'settings-card__badge--success' : 'settings-card__badge--muted'}`}>{settings?.steamWebApiKeyConfigured ? '已配置' : '未配置'}</span>
        </div>
        <p className="settings-card__description">控制服务加密保存此密钥，用于补全大厅成员的公开资料。密钥不会回显。</p>
        <form className="form-grid settings-card__form" onSubmit={saveSteamWebApiKey}>
          <label className="form-field form-field--wide">Steam Web API 密钥
            <input aria-label="Steam Web API 密钥" onChange={event => setSteamWebApiKey(event.target.value)} placeholder={settings?.steamWebApiKeyConfigured ? '已配置，输入新密钥以替换' : '输入 Steam Web API 密钥'} type="password" value={steamWebApiKey} />
            <small>当前状态：{settings?.steamWebApiKeyConfigured ? '已配置' : '未配置'}</small>
          </label>
          <div className="form-actions form-field--wide">
            <button className="button button--primary" disabled={keySaving || !steamWebApiKey.trim()} type="submit">{keySaving ? '正在保存' : '保存 Steam Web API 密钥'}</button>
            {settings?.steamWebApiKeyConfigured ? <button className="button button--quiet" disabled={keySaving} onClick={() => void clearSteamWebApiKey()} type="button">清除 Steam Web API 密钥</button> : null}
          </div>
        </form>
        {keyError && <p className="inline-error" role="alert">{keyError}</p>}
        {keyMessage && <p className="success-message" role="status">{keyMessage}</p>}
        {settings && <small className="settings-card__updated">上次更新：{formatUpdatedAt(settings.updatedAt)}</small>}
      </article>
    </div>
    <ConfirmDialog
      confirmLabel="确认禁用"
      description="控制服务会停止并清空所有当前暖服任务。暖服节点容器不会停止，之后可以重新启用调度。"
      intent="danger"
      isLoading={schedulingSaving}
      isOpen={confirmDisable}
      onClose={() => setConfirmDisable(false)}
      onConfirm={() => { setConfirmDisable(false); void saveWarmupScheduling(false); }}
      title="确认禁用暖服和调度"
    />
  </section>;
}

function formatUpdatedAt(value: string) {
  return new Date(value).toLocaleString('zh-CN', { hour12: false });
}
