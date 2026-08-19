import { useEffect, useState, type FormEvent } from 'react';
import type { GlobalSettings } from '../../api/models';

export function SettingsWorkspace({ getSettings, updateSettings }: { getSettings: () => Promise<GlobalSettings>; updateSettings: (input: { steamProxyUrl: string | null }) => Promise<GlobalSettings> }) {
  const [settings, setSettings] = useState<GlobalSettings | null>(null);
  const [proxy, setProxy] = useState('');
  const [busy, setBusy] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { let active = true; void getSettings().then(value => { if (!active) return; setSettings(value); setProxy(value.steamProxyUrl ?? ''); }).catch(() => { if (active) setError('读取全局设置失败，请重试。'); }).finally(() => { if (active) setBusy(false); }); return () => { active = false; }; }, []);
  const save = async (event: FormEvent) => { event.preventDefault(); setSaving(true); setError(null); setMessage(null); try { const value = await updateSettings({ steamProxyUrl: proxy.trim() || null }); setSettings(value); setProxy(value.steamProxyUrl ?? ''); setMessage('全局设置已保存。重建已启用 VNC 的暖服节点后生效。'); } catch (caught) { setError(caught instanceof Error ? caught.message : '保存全局设置失败。'); } finally { setSaving(false); } };
  if (busy) return <p className="empty-state">正在读取全局设置</p>;
  return <section className="editor-panel settings-panel"><h2>Steam 代理</h2><p>代理仅用于开启 VNC 服务的暖服节点，加速通过 VNC 完成 Steam 配置；不会代理控制服务或关闭 VNC 的节点。</p><form className="form-grid" onSubmit={save}><label className="form-field form-field--wide">代理地址<input aria-label="Steam 代理地址" onChange={event => setProxy(event.target.value)} placeholder="http://127.0.0.1:7890" value={proxy} /><small>支持 HTTP 或 HTTPS 代理，例如 http://127.0.0.1:7890。留空表示不配置。</small></label>{error && <p className="inline-error form-field--wide">{error}</p>}{message && <p className="success-message form-field--wide">{message}</p>}<div className="form-actions form-field--wide"><button className="button button--primary" disabled={saving} type="submit">{saving ? '正在保存' : '保存设置'}</button></div></form>{settings && <small className="settings-panel__updated">上次更新：{new Date(settings.updatedAt).toLocaleString('zh-CN', { hour12: false })}</small>}</section>;
}
