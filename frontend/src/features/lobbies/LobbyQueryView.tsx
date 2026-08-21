import { Check, Copy, LoaderCircle, Search, User } from 'lucide-react'; import { useState, type FormEvent } from 'react';
import type { LobbySnapshot } from '../../api/models';
import { LobbyMetadataTable } from './LobbyMetadataTable';

interface LobbyQueryViewProps {
  queryLobby: (lobbyId: string) => Promise<LobbySnapshot>;
}

const maxLobbyId = 18_446_744_073_709_551_615n;

function normalizeLobbyId(input: string): string | null {
  const value = input.trim();
  if (!value || !/^[0-9a-f]+$/i.test(value)) return null;
  if (/^\d{18}$/.test(value)) {
    return value === '0'.repeat(18) ? null : value;
  }
  try {
    const decimal = BigInt(`0x${value}`);
    return decimal > 0n && decimal <= maxLobbyId ? decimal.toString(10) : null;
  } catch {
    return null;
  }
}

const failureCopy: Record<string, string> = { lobby_data_unavailable: '大厅数据暂时不可用。', lobby_operation_preservation_failed: '大厅操作未能确认保存。', lobby_query_agent_unavailable: '查询节点当前不可用。' };

export function LobbyQueryView({ queryLobby }: LobbyQueryViewProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lobbyId, setLobbyId] = useState('');
  const [result, setResult] = useState<LobbySnapshot | null>(null);
  const [stale, setStale] = useState(false);
  const [copied, setCopied] = useState(false);

  const joinUri = result?.ownerSteamId ? `steam://joinlobby/500/${result.lobbyId}/${result.ownerSteamId}` : null;
  const copyJoinUri = async () => {
    if (!joinUri) return;
    await navigator.clipboard.writeText(joinUri);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1800);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalizedId = normalizeLobbyId(lobbyId);
    if (!normalizedId) {
      setError('请输入非零十六进制或 18 位十进制大厅 ID。');
      return;
    }
    setBusy(true); setError(null);
    try {
      const snapshot = await queryLobby(normalizedId);
      setResult(snapshot); setStale(false); setCopied(false);
    } catch (caught) {
      const code = caught instanceof Error ? caught.message : 'lobby_data_unavailable';
      setStale(result !== null);
      setError(`${failureCopy[code] ?? '大厅查询失败。'}${result ? ' 结果可能已过期。' : ''}（${code}）`);
    } finally {
      setBusy(false);
    }
  };

  const complete = result?.memberDataStatus === 'complete';
  return <div className="lobby-query"><form aria-label="大厅查询表单" onSubmit={event => void submit(event)}><label className="form-field">大厅 ID<input aria-label="大厅 ID" inputMode="text" onChange={event => setLobbyId(event.target.value)} placeholder="输入 Steam 大厅 ID（十进制或十六进制）" value={lobbyId} /></label><button className="button button--primary" disabled={busy} type="submit">{busy ? <><LoaderCircle className="spin" size={16} />正在查询</> : <><Search size={16} />查询大厅</>}</button></form>{error ? <p className="inline-error">{error}</p> : null}{result ? <section aria-label="大厅查询结果" className={`lobby-result${stale ? ' is-stale' : ''}`}>{stale && <p className="stale-notice">结果可能已过期</p>}<h2>查询结果</h2><dl className="lobby-result__overview"><div><dt>大厅 ID</dt><dd>{result.lobbyId}</dd></div><div><dt>所有者 Steam ID</dt><dd><code>{result.ownerSteamId ?? '未知'}</code></dd></div><div><dt>成员状态</dt><dd>{complete ? '成员数据完整' : '成员数据未确认'}</dd></div><div><dt>已确认成员数</dt><dd>{complete ? result.members.length : '成员数据未确认'}</dd></div><div><dt>观测时间</dt><dd>{new Date(result.observedAt).toLocaleString('zh-CN', { hour12: false })}</dd></div></dl><div className="lobby-join-uri"><label>一键加入大厅 URI<input aria-label="Steam 大厅加入 URI" readOnly value={joinUri ?? '当前结果未返回房主 Steam ID，暂时无法生成完整加入 URI。'} /></label>{joinUri ? <button aria-label="复制加入 URI" onClick={() => void copyJoinUri()} type="button">{copied ? <><Check size={16} />已复制</> : <><Copy size={16} />复制加入 URI</>}</button> : null}</div><section className="lobby-members"><h2>成员列表</h2>{complete ? result.members.map(member => <div key={member.steamId}><span className="lobby-member__avatar">{member.avatarUrl ? <img alt={`${member.personaName ?? member.steamId} 头像`} src={member.avatarUrl} /> : <User aria-label="未知玩家头像" size={18} />}</span><code title={member.steamId}>{member.steamId}</code><span>{member.personaName ?? '未知玩家'}</span></div>) : <p>成员数据未确认</p>}</section><LobbyMetadataTable metadata={result.metadata} /></section> : null}</div>;
}
