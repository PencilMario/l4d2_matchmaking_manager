import { LoaderCircle, Search } from 'lucide-react'; import { useState, type FormEvent } from 'react';
import type { LobbySnapshot } from '../../api/models';
import { LobbyMetadataTable } from './LobbyMetadataTable';

interface LobbyQueryViewProps {
  queryLobby: (lobbyId: string) => Promise<LobbySnapshot>;
}

const failureCopy: Record<string, string> = { lobby_data_unavailable: '大厅数据暂时不可用。', lobby_operation_preservation_failed: '大厅操作未能确认保存。', lobby_query_agent_unavailable: '查询节点当前不可用。' };

export function LobbyQueryView({ queryLobby }: LobbyQueryViewProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lobbyId, setLobbyId] = useState('');
  const [result, setResult] = useState<LobbySnapshot | null>(null);
  const [stale, setStale] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalizedId = lobbyId.trim();
    if (!/^[1-9]\d*$/.test(normalizedId)) {
      setError('请输入非零十进制大厅 ID。');
      return;
    }
    setBusy(true); setError(null);
    try {
      const snapshot = await queryLobby(normalizedId);
      setResult(snapshot); setStale(false);
    } catch (caught) {
      const code = caught instanceof Error ? caught.message : 'lobby_data_unavailable';
      setStale(result !== null);
      setError(`${failureCopy[code] ?? '大厅查询失败。'}${result ? ' 结果可能已过期。' : ''}（${code}）`);
    } finally {
      setBusy(false);
    }
  };

  const complete = result?.memberDataStatus === 'complete';
  return <div className="lobby-query"><form aria-label="大厅查询表单" onSubmit={event => void submit(event)}><label className="form-field">大厅 ID<input aria-label="大厅 ID" inputMode="numeric" onChange={event => setLobbyId(event.target.value)} placeholder="输入 Steam 大厅 ID" value={lobbyId} /></label><button className="button button--primary" disabled={busy} type="submit">{busy ? <><LoaderCircle className="spin" size={16} />正在查询</> : <><Search size={16} />查询大厅</>}</button></form>{error ? <p className="inline-error">{error}</p> : null}{result ? <section aria-label="大厅查询结果" className={`lobby-result${stale ? ' is-stale' : ''}`}>{stale && <p className="stale-notice">结果可能已过期</p>}<h2>查询结果</h2><dl className="lobby-result__overview"><div><dt>大厅 ID</dt><dd>{result.lobbyId}</dd></div><div><dt>所有者 Steam ID</dt><dd><code>{result.ownerSteamId ?? '未知'}</code></dd></div><div><dt>成员状态</dt><dd>{complete ? '成员数据完整' : '成员数据未确认'}</dd></div><div><dt>已确认成员数</dt><dd>{complete ? result.members.length : '成员数据未确认'}</dd></div><div><dt>观测时间</dt><dd>{new Date(result.observedAt).toLocaleString('zh-CN', { hour12: false })}</dd></div></dl><section className="lobby-members"><h2>成员列表</h2>{complete ? result.members.map(member => <div key={member.steamId}><code title={member.steamId}>{member.steamId}</code><span>{member.personaName ?? '未知玩家'}</span></div>) : <p>成员数据未确认</p>}</section><LobbyMetadataTable metadata={result.metadata} /></section> : null}</div>;
}
