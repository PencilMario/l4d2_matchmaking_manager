import { useMemo, useState, type FormEvent } from 'react';
import { CoreClient } from './api/core-client';
import { WorkspaceShell } from './features/workspace/WorkspaceShell';

export default function App() {
  const [tokenInput, setTokenInput] = useState('');
  const [token, setToken] = useState<string | null>(null);
  const client = useMemo(() => new CoreClient(() => token), [token]);

  const connect = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (tokenInput.trim()) setToken(tokenInput.trim());
  };

  if (token) return <WorkspaceShell client={client} onUnauthorized={() => setToken(null)} />;

  return (
    <main className="app-bootstrap">
      <form className="app-bootstrap__surface" onSubmit={connect} aria-label="管理工作区">
        <p className="app-bootstrap__eyebrow">CORE CONTROLLER / LOCAL ACCESS</p>
        <h1>L4D2 Matchmaking Manager</h1>
        <p className="app-bootstrap__label">管理工作区</p>
        <label className="app-bootstrap__token">CORE API TOKEN<input autoFocus onChange={event => setTokenInput(event.target.value)} type="password" value={tokenInput} /></label>
        <button type="submit">连接 Core</button>
        <div className="app-bootstrap__status" aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
      </form>
    </main>
  );
}
