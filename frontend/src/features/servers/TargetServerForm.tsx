import { useMemo, useState, type FormEvent } from 'react';
import type { TargetServer, TargetServerGameMode, TargetServerInput } from '../../api/models';

interface Props { busy?: boolean; error?: string | null; onCancel?: () => void; onSubmit: (input: TargetServerInput) => void; server?: TargetServer; }
interface Values { endpoint: string; requiresReservation: boolean; enabled: boolean; priority: string; maxConcurrentWarmups: string; attemptWindowSeconds: string; playerTarget: string; gameMode: TargetServerGameMode | ''; rconPassword: string; clearRcon: boolean; }
const defaults = (server?: TargetServer): Values => ({ endpoint: server?.endpoint ?? '', requiresReservation: false, enabled: server?.enabled ?? true, priority: String(server?.priority ?? 0), maxConcurrentWarmups: String(server?.maxConcurrentWarmups ?? 36), attemptWindowSeconds: String(server?.attemptWindowSeconds ?? 720), playerTarget: String(server?.playerTarget ?? 4), gameMode: server?.gameMode ?? '', rconPassword: '', clearRcon: false });
const integer = (value: string) => value.trim() === '' ? null : Number(value);

export function TargetServerForm({ busy = false, error, onCancel, onSubmit, server }: Props) {
  const [values, setValues] = useState(() => defaults(server));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const minutes = useMemo(() => Math.max(0, Number(values.attemptWindowSeconds || 0) / 60), [values.attemptWindowSeconds]);
  const update = <K extends keyof Values>(key: K, value: Values[K]) => setValues(current => ({ ...current, [key]: value }));
  const submit = (event: FormEvent) => { event.preventDefault(); const next: Record<string, string> = {};
    if (!values.endpoint.trim()) next.endpoint = '请输入服务器地址。';
    for (const [key, label] of [['maxConcurrentWarmups', '最大并发暖服数'], ['attemptWindowSeconds', '单次暖服时限'], ['playerTarget', '目标玩家数']] as const) if (!Number.isInteger(Number(values[key])) || Number(values[key]) <= 0) next[key] = `${label}必须为大于 0 的整数。`;
    if (!Number.isInteger(Number(values.priority))) next.priority = '调度优先级必须为整数。';
    setErrors(next); if (Object.keys(next).length) return;
    onSubmit({ endpoint: values.endpoint.trim(), requiresReservation: false, priority: integer(values.priority), maxConcurrentWarmups: integer(values.maxConcurrentWarmups), attemptWindowSeconds: integer(values.attemptWindowSeconds), playerTarget: integer(values.playerTarget), gameMode: values.gameMode || null, enabled: values.enabled, rconPassword: null });
  };
  return <form aria-label="目标服务器配置表单" className="form-grid" onSubmit={submit}>
    <label className="form-field form-field--wide">服务器地址<input aria-describedby="endpoint-help" aria-invalid={Boolean(errors.endpoint)} aria-label="服务器地址" onChange={event => update('endpoint', event.target.value)} placeholder="203.0.113.10:27015" value={values.endpoint} /><small id="endpoint-help">支持主机名或 IPv4 地址加游戏端口</small>{errors.endpoint && <em>{errors.endpoint}</em>}</label>
    <div className="switch-field"><span><b>L4D1 标准大厅</b><small>预约握手尚未真机验证，当前版本已禁用</small></span></div>
    <label className="switch-field"><input aria-label="启用调度" checked={values.enabled} onChange={event => update('enabled', event.target.checked)} role="switch" type="checkbox" /><span><b>启用调度</b><small>关闭后不会再分配新的暖服任务</small></span></label>
    <label className="form-field"><span>模式类型</span><select aria-label="模式类型" onChange={event => update('gameMode', event.target.value as TargetServerGameMode | '')} value={values.gameMode}><option value="">未指定（默认 coop）</option><option value="versus">versus</option><option value="coop">coop</option></select><small>未指定时使用已验证的 L4D1 coop 大厅 metadata</small></label>
    <NumberField error={errors.priority} label="调度优先级" onChange={value => update('priority', value)} value={values.priority}>数值越高越优先，可以为负数</NumberField>
    <NumberField error={errors.maxConcurrentWarmups} label="最大并发暖服数" onChange={value => update('maxConcurrentWarmups', value)} value={values.maxConcurrentWarmups}>普通服务器默认 36</NumberField>
    <NumberField error={errors.attemptWindowSeconds} label="单次暖服时限" onChange={value => update('attemptWindowSeconds', value)} value={values.attemptWindowSeconds}>默认 720 秒，约 {minutes} 分钟</NumberField>
    <NumberField error={errors.playerTarget} label="目标玩家数" onChange={value => update('playerTarget', value)} value={values.playerTarget}>默认 4 人</NumberField>
    {error && <p className="inline-error form-field--wide">{error}</p>}<div className="form-actions form-field--wide">{onCancel && <button className="button button--quiet" disabled={busy} onClick={onCancel} type="button">取消</button>}<button className="button button--primary" disabled={busy} type="submit">{busy ? '正在保存' : server ? '保存设置' : '新增服务器'}</button></div>
  </form>;
}
function NumberField({ label, value, onChange, disabled, error, children }: { label: string; value: string; onChange: (value: string) => void; disabled?: boolean; error?: string; children: React.ReactNode }) { return <label className="form-field">{label}<input aria-invalid={Boolean(error)} aria-label={label} disabled={disabled} onChange={event => onChange(event.target.value)} step="1" type="number" value={value} /><small>{children}</small>{error && <em>{error}</em>}</label>; }
