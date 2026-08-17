import { useState, type FormEvent } from 'react';
import type { LobbySnapshot } from '../../api/models';
import { LobbyMetadataTable } from './LobbyMetadataTable';

interface LobbyQueryViewProps {
  queryLobby: (lobbyId: string) => Promise<LobbySnapshot>;
}

const failureCopy: Record<string, string> = {
  lobby_data_unavailable: 'Lobby 数据暂时不可用；上次结果已过期。',
  lobby_operation_preservation_failed: 'Lobby 操作保存失败；上次结果已过期。',
  lobby_query_agent_unavailable: '查询 Agent 当前不可用；上次结果已过期。',
};

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
      setError('请输入非零十进制 Lobby ID。');
      return;
    }
    setBusy(true); setError(null);
    try {
      const snapshot = await queryLobby(normalizedId);
      setResult(snapshot); setStale(false);
    } catch (caught) {
      const code = caught instanceof Error ? caught.message : 'lobby_data_unavailable';
      setStale(result !== null);
      setError(failureCopy[code] ?? `Lobby 查询失败：${code}`);
    } finally {
      setBusy(false);
    }
  };

  const complete = result?.memberDataStatus === 'complete';
  return <div className="lobby-query"><form aria-label="Lobby 查询表单" onSubmit={event => void submit(event)}><label>Lobby ID<input aria-label="Lobby ID" inputMode="numeric" onChange={event => setLobbyId(event.target.value)} placeholder="Steam lobby ID" value={lobbyId} /></label><button className="command-button" disabled={busy} type="submit">{busy ? '查询中...' : '查询 Lobby'}</button></form>{error ? <p className="lobby-query__error">{error}</p> : null}{result ? <section aria-label="Lobby 查询结果" className={`lobby-result${stale ? ' is-stale' : ''}`}><header><div><span>LOBBY</span><strong>{result.lobbyId}</strong></div><small>{stale ? '结果已过期' : '当前查询结果'}</small></header><div className="lobby-result__overview"><div><span>OWNER</span><code>{result.ownerSteamId ?? '未知'}</code></div><div><span>MEMBERS</span><strong>{complete ? `确认成员 ${result.members.length}` : '成员数 不可用'}</strong></div><div><span>DATA</span><strong>{complete ? '成员数据完整' : '成员数据未确认'}</strong></div></div>{complete ? <section aria-label="Lobby 成员" className="lobby-members">{result.members.map(member => <div key={member.steamId}><code>{member.steamId}</code><span>{member.personaName ?? '未知玩家'}</span></div>)}</section> : <p className="lobby-result__notice">{result.memberDataStatus}</p>}<LobbyMetadataTable metadata={result.metadata} /></section> : null}</div>;
}
