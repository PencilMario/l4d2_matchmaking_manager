import { useState, type FormEvent } from 'react';
import type { TargetServer, TargetServerInput } from '../../api/models';
import SpecularButton from '../../components/react-bits/SpecularButton/SpecularButton';

interface TargetServerFormProps {
  busy?: boolean;
  error?: string | null;
  onCancel?: () => void;
  onSubmit: (input: TargetServerInput) => void;
  server: TargetServer;
}

interface FormValues {
  attemptWindowSeconds: string;
  enabled: boolean;
  endpoint: string;
  maxConcurrentWarmups: string;
  playerTarget: string;
  priority: string;
  rconPassword: string;
  requiresReservation: boolean;
}

function initialValues(server: TargetServer): FormValues {
  return {
    attemptWindowSeconds: String(server.attemptWindowSeconds),
    enabled: server.enabled,
    endpoint: server.endpoint,
    maxConcurrentWarmups: String(server.maxConcurrentWarmups),
    playerTarget: String(server.playerTarget),
    priority: String(server.priority),
    rconPassword: '',
    requiresReservation: server.requiresReservation,
  };
}

function nullableInteger(value: string): number | null {
  const normalized = value.trim();
  return normalized === '' ? null : Number(normalized);
}

export function TargetServerForm({ busy = false, error, onCancel, onSubmit, server }: TargetServerFormProps) {
  const [values, setValues] = useState<FormValues>(() => initialValues(server));
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSubmit({
      endpoint: values.endpoint.trim(),
      requiresReservation: values.requiresReservation,
      priority: nullableInteger(values.priority),
      maxConcurrentWarmups: nullableInteger(values.maxConcurrentWarmups),
      attemptWindowSeconds: nullableInteger(values.attemptWindowSeconds),
      playerTarget: nullableInteger(values.playerTarget),
      enabled: values.enabled,
      rconPassword: values.requiresReservation && values.rconPassword.length > 0 ? values.rconPassword : null,
    });
  };

  return <form aria-label="Target Server 配置表单" className="target-server-form" onSubmit={submit}>
    <label><span>endpoint</span><input aria-label="endpoint" onChange={event => setValues(current => ({ ...current, endpoint: event.target.value }))} required value={values.endpoint} /></label>
    <div className="target-server-form__switches"><label><input aria-label="预留大厅" checked={values.requiresReservation} onChange={event => setValues(current => ({ ...current, requiresReservation: event.target.checked, rconPassword: event.target.checked ? current.rconPassword : '' }))} type="checkbox" />预留大厅</label><label><input aria-label="启用调度" checked={values.enabled} onChange={event => setValues(current => ({ ...current, enabled: event.target.checked }))} type="checkbox" />启用调度</label></div>
    <div className="target-server-form__numbers"><NumberField label="priority" onChange={value => setValues(current => ({ ...current, priority: value }))} value={values.priority} /><NumberField label="maxConcurrentWarmups" onChange={value => setValues(current => ({ ...current, maxConcurrentWarmups: value }))} value={values.maxConcurrentWarmups} /><NumberField label="attemptWindowSeconds" onChange={value => setValues(current => ({ ...current, attemptWindowSeconds: value }))} value={values.attemptWindowSeconds} /><NumberField label="playerTarget" onChange={value => setValues(current => ({ ...current, playerTarget: value }))} value={values.playerTarget} /></div>
    {values.requiresReservation ? <label className="target-server-form__rcon"><span>RCON 密码</span><input aria-label="RCON 密码" autoComplete="new-password" onChange={event => setValues(current => ({ ...current, rconPassword: event.target.value }))} placeholder={server.hasRconCredentials ? '留空会清除当前凭据' : '仅写入，不会回填'} type="password" value={values.rconPassword} /><small>密码为只写字段，读取结果不会包含它。</small></label> : null}
    {error ? <p className="target-server-form__error">{error}</p> : null}
    <div className="target-server-form__actions">{onCancel ? <SpecularButton className="target-server-form__cancel" onClick={onCancel}>取消</SpecularButton> : null}<SpecularButton disabled={busy} type="submit">{busy ? '保存中...' : '保存配置'}</SpecularButton></div>
  </form>;
}

function NumberField({ label, onChange, value }: { label: 'priority' | 'maxConcurrentWarmups' | 'attemptWindowSeconds' | 'playerTarget'; onChange: (value: string) => void; value: string }) {
  return <label><span>{label}</span><input aria-label={label} inputMode="numeric" onChange={event => onChange(event.target.value)} type="number" value={value} /></label>;
}
