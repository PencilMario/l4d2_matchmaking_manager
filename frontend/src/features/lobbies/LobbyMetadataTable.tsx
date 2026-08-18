export function LobbyMetadataTable({ metadata }: { metadata: Record<string, string> }) {
  const entries = Object.entries(metadata).sort(([left], [right]) => left.localeCompare(right));
  return <section aria-label="会话元数据" className="lobby-metadata"><h2>会话元数据</h2><div><span>键</span><span>值</span></div>{entries.length === 0 ? <p>未返回 metadata。</p> : entries.map(([key, value]) => <div key={key}><code title={key}>{key}</code><code title={value}>{value}</code></div>)}</section>;
}
