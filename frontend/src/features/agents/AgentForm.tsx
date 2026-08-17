import { useState, type FormEvent } from 'react';
import type { WarmupAgent, WarmupAgentInput } from '../../api/models';
import SpecularButton from '../../components/react-bits/SpecularButton/SpecularButton';

interface AgentFormProps {
  busy?: boolean;
  error?: string | null;
  onCancel?: () => void;
  onSubmit: (input: WarmupAgentInput) => void;
  agent?: WarmupAgent;
}

export function AgentForm({ agent, busy = false, error, onCancel, onSubmit }: AgentFormProps) {
  const [name, setName] = useState(agent?.name ?? '');
  const [downloadRegion, setDownloadRegion] = useState(agent?.downloadRegion ?? '');
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSubmit({ name: name.trim(), downloadRegion: downloadRegion.trim() || null });
  };
  return <form aria-label="Warm-up Agent 表单" className="agent-form" onSubmit={submit}><label>Agent 名称<input aria-label="Agent 名称" onChange={event => setName(event.target.value)} required value={name} /></label><label>下载区域<input aria-label="下载区域" onChange={event => setDownloadRegion(event.target.value)} value={downloadRegion} /></label>{error ? <p className="target-server-form__error">{error}</p> : null}<div>{onCancel ? <SpecularButton className="agent-form__cancel" onClick={onCancel}>取消</SpecularButton> : null}<SpecularButton disabled={busy} type="submit">{busy ? '保存中...' : agent ? '保存 Agent' : '创建 Agent'}</SpecularButton></div></form>;
}
