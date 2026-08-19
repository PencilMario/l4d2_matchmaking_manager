import { useEffect, useState, type FormEvent } from 'react';

export function GlobalSettingsView({ load, save }: { load: () => Promise<{ steamProxyUrl: string | null; updatedAt: string }>; save: (proxy: string | null) => Promise<{ steamProxyUrl: string | null; updatedAt: string }> }) {
  const [proxy, setProxy] = useState('');
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { let active = true; void load().then(value => { if (active) { setProxy(value.steamProxyUrl ?? ''); setUpdatedAt(value.updatedAt); } }).catch(() => { if (active) setError('读取全局设置失败，请重试。'); }).finally(() => { if (active) setLoading(false); }); return () => { active = false; }; }, []);
  const submit = async (event: FormEvent) => { event.preventDefault(); setSaving(true); setError(null); setMessage(null); try { const value = await save(proxy.trim() || null); setProxy(value.steamProxyUrl ?? ''); setUpdatedAt(value.updatedAt); setMessage('全局设置已保存。重建已启用 VNC 的暖服节点后生效。'); } catch (caught) { setError(caught instanceof Error ? caught.message : '保存全局设置失败。'); } finally { setSaving(false); } };
  if (loading) return <section><h1 className="text-xl font-bold text-slate-900">全局设置</h1><p className="mt-4 text-sm text-slate-500">正在读取全局设置</p></section>;
  return <section className="space-y-4"><h1 className="text-xl font-bold text-slate-900">全局设置</h1><div className="max-w-2xl rounded-lg border border-slate-200 bg-white p-5 shadow-xs"><h2 className="text-sm font-semibold text-slate-900">Steam 代理</h2><p className="mt-1 text-xs leading-5 text-slate-500">代理仅用于开启 VNC 服务的暖服节点，加速通过 VNC 完成 Steam 配置；不会代理控制服务或关闭 VNC 的节点。</p><form className="mt-5 space-y-3" onSubmit={submit}><label className="block text-xs font-medium text-slate-700">代理地址<input aria-label="Steam 代理地址" className="mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-xs" onChange={event => setProxy(event.target.value)} placeholder="http://127.0.0.1:7890" value={proxy} /><span className="mt-1 block text-[11px] text-slate-500">支持 HTTP 或 HTTPS 代理。留空表示不配置。</span></label>{error && <p className="text-xs text-red-700">{error}</p>}{message && <p className="text-xs text-emerald-700">{message}</p>}<button className="rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white disabled:opacity-50" disabled={saving} type="submit">{saving ? '正在保存' : '保存设置'}</button></form>{updatedAt && <p className="mt-4 text-[11px] text-slate-400">上次更新：{new Date(updatedAt).toLocaleString('zh-CN', { hour12: false })}</p>}</div></section>;
}
