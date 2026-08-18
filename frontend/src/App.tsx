import { useMemo, useState, type FormEvent } from 'react';
import { Eye, EyeOff, LoaderCircle } from 'lucide-react';
import { CoreClient } from './api/core-client';
import { WorkspaceShell } from './features/workspace/WorkspaceShell';

export default function App() {
  const [tokenInput, setTokenInput] = useState('');
  const [token, setToken] = useState<string | null>(null);
  const [visible, setVisible] = useState(false);
  const [connecting, setConnecting] = useState(false);
  const client = useMemo(() => new CoreClient(() => token), [token]);

  const connect = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!tokenInput.trim()) return;
    setConnecting(true);
    setToken(tokenInput.trim());
  };

  if (token) return <WorkspaceShell client={client} onUnauthorized={() => { setToken(null); setConnecting(false); }} onSignOut={() => { setToken(null); setTokenInput(''); setConnecting(false); }} />;

  return (
    <main className="app-bootstrap">
      <form className="login-panel" onSubmit={connect} aria-label="登录表单">
        <div className="login-panel__mark">L4D2</div><h1>L4D2 匹配管理</h1><p>连接控制服务后，可统一管理暖服节点、大厅与目标服务器。</p>
        <label>访问令牌<div className="password-field"><input autoFocus onChange={event => setTokenInput(event.target.value)} type={visible ? 'text' : 'password'} value={tokenInput} /><button aria-label={visible ? '隐藏令牌' : '显示令牌'} onClick={() => setVisible(value => !value)} title={visible ? '隐藏令牌' : '显示令牌'} type="button">{visible ? <EyeOff size={17} /> : <Eye size={17} />}</button></div></label>
        <button className="button button--primary login-panel__submit" disabled={!tokenInput.trim() || connecting} type="submit">{connecting ? <><LoaderCircle className="spin" size={16} />正在连接</> : '进入管理后台'}</button>
      </form>
    </main>
  );
}
