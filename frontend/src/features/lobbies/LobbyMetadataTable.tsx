export function LobbyMetadataTable({ metadata }: { metadata: Record<string, string> }) {
  const entries = Object.entries(metadata).sort(([left], [right]) => left.localeCompare(right));
  return <section aria-label="Lobby metadata" className="lobby-metadata"><div><span>KEY</span><span>VALUE</span></div>{entries.length === 0 ? <p>未返回 metadata。</p> : entries.map(([key, value]) => <div key={key}><code>{key}</code><code>{value}</code></div>)}</section>;
}
