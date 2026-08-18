import { useMemo, useState, type FormEvent } from 'react';
import type { TargetServer, TargetServerInput } from '../../api/models';

interface Props { busy?: boolean; error?: string | null; onCancel?: () => void; onSubmit: (input: TargetServerInput) => void; server?: TargetServer; }
interface Values { endpoint: string; requiresReservation: boolean; enabled: boolean; priority: string; maxConcurrentWarmups: string; attemptWindowSeconds: string; playerTarget: string; rconPassword: string; clearRcon: boolean; }
const defaults = (server?: TargetServer): Values => ({ endpoint: server?.endpoint ?? '', requiresReservation: server?.requiresReservation ?? false, enabled: server?.enabled ?? true, priority: String(server?.priority ?? 0), maxConcurrentWarmups: String(server?.maxConcurrentWarmups ?? 36), attemptWindowSeconds: String(server?.attemptWindowSeconds ?? 720), playerTarget: String(server?.playerTarget ?? 6), rconPassword: '', clearRcon: false });
const integer = (value: string) => value.trim() === '' ? null : Number(value);

export function TargetServerForm({ busy = false, error, onCancel, onSubmit, server }: Props) {
  const [values, setValues] = useState(() => defaults(server));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const minutes = useMemo(() => Math.max(0, Number(values.attemptWindowSeconds || 0) / 60), [values.attemptWindowSeconds]);
  const update = <K extends keyof Values>(key: K, value: Values[K]) => setValues(current => ({ ...current, [key]: value }));
  const submit = (event: FormEvent) => { event.preventDefault(); const next: Record<string, string> = {};
    if (!values.endpoint.trim()) next.endpoint = '请输入服务器地址。';
    for (const [key, label] of [['maxConcurrentWarmups', '最大并发暖服数'], ['attemptWindowSeconds', '单次暖服时限'], ['playerTarget', '目标玩家数']] as const) if (!values.requiresReservation || key !== 'maxConcurrentWarmups') if (!Number.isInteger(Number(values[key])) || Number(values[key]) <= 0) next[key] = `${label}必须为大于 0 的整数。`;
    if (!Number.isInteger(Number(values.priority))) next.priority = '调度优先级必须为整数。';
    setErrors(next); if (Object.keys(next).length) return;
    onSubmit({ endpoint: values.endpoint.trim(), requiresReservation: values.requiresReservation, priority: integer(values.priority), maxConcurrentWarmups: values.requiresReservation ? 1 : integer(values.maxConcurrentWarmups), attemptWindowSeconds: integer(values.attemptWindowSeconds), playerTarget: integer(values.playerTarget), enabled: values.enabled, rconPassword: values.requiresReservation && values.rconPassword ? values.rconPassword : values.requiresReservation && values.clearRcon ? null : null });
  };
  return <form aria-label="目标服务器配置表单" className="form-grid" onSubmit={submit}>
    <label className="form-field form-field--wide">服务器地址<input aria-describedby="endpoint-help" aria-invalid={Boolean(errors.endpoint)} aria-label="服务器地址" onChange={event => update('endpoint', event.target.value)} placeholder="203.0.113.10:27015" value={values.endpoint} /><small id="endpoint-help">支持主机名或 IPv4 地址加游戏端口</small>{errors.endpoint && <em>{errors.endpoint}</em>}</label>
    <label className="switch-field"><input aria-label="需要大厅预留" checked={values.requiresReservation} onChange={event => update('requiresReservation', event.target.checked)} role="switch" type="checkbox" /><span><b>需要大厅预留</b><small>开启后可配置 RCON 密码</small></span></label>
    <label className="switch-field"><input aria-label="启用调度" checked={values.enabled} onChange={event => update('enabled', event.target.checked)} role="switch" type="checkbox" /><span><b>启用调度</b><small>关闭后不会再分配新的暖服任务</small></span></label>
    <NumberField error={errors.priority} label="调度优先级" onChange={value => update('priority', value)} value={values.priority}>数值越高越优先，可以为负数</NumberField>
    <NumberField disabled={values.requiresReservation} error={errors.maxConcurrentWarmups} label="最大并发暖服数" onChange={value => update('maxConcurrentWarmups', value)} value={values.requiresReservation ? '1' : values.maxConcurrentWarmups}>{values.requiresReservation ? '预留服务器固定为 1' : '普通服务器默认 36'}</NumberField>
    <NumberField error={errors.attemptWindowSeconds} label="单次暖服时限" onChange={value => update('attemptWindowSeconds', value)} value={values.attemptWindowSeconds}>默认 720 秒，约 {minutes} 分钟</NumberField>
    <NumberField error={errors.playerTarget} label="目标玩家数" onChange={value => update('playerTarget', value)} value={values.playerTarget}>默认 6 人</NumberField>
    {values.requiresReservation && <label className="form-field form-field--wide">RCON 密码<input aria-label="RCON 密码" autoComplete="new-password" onChange={event => { update('rconPassword', event.target.value); update('clearRcon', false); }} placeholder={server?.hasRconCredentials ? '已配置，输入新密码可覆盖' : '仅写入，不会回填'} type="password" value={values.rconPassword} /><small>{server?.hasRconCredentials ? '当前 RCON 密码已配置。勾选清空将移除凭据。' : '密码只写入控制服务，不会回填。'}</small>{server?.hasRconCredentials && <span className="check-inline"><input aria-label="清空 RCON 凭据" checked={values.clearRcon} onChange={event => update('clearRcon', event.target.checked)} type="checkbox" />清空意味着移除凭据</span>}</label>}
    {error && <p className="inline-error form-field--wide">{error}</p>}<div className="form-actions form-field--wide">{onCancel && <button className="button button--quiet" disabled={busy} onClick={onCancel} type="button">取消</button>}<button className="button button--primary" disabled={busy} type="submit">{busy ? '正在保存' : server ? '保存设置' : '新增服务器'}</button></div>
  </form>;
}
function NumberField({ label, value, onChange, disabled, error, children }: { label: string; value: string; onChange: (value: string) => void; disabled?: boolean; error?: string; children: React.ReactNode }) { return <label className="form-field">{label}<input aria-invalid={Boolean(error)} aria-label={label} disabled={disabled} onChange={event => onChange(event.target.value)} step="1" type="number" value={value} /><small>{children}</small>{error && <em>{error}</em>}</label>; }
