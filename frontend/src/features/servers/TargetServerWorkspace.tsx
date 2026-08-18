import { useState } from 'react';
import type { TargetServer, TargetServerInput, TargetServerObservation, WarmupStatus } from '../../api/models';
import SpecularButton from '../../components/react-bits/SpecularButton/SpecularButton';
import { TargetServerForm } from './TargetServerForm';
import { TargetServerTable } from './TargetServerTable';

interface TargetServerWorkspaceProps {
  observations: TargetServerObservation[];
  onCreate?: (input: TargetServerInput) => Promise<void>;
  onOpenServer?: (server: TargetServer) => void;
  onRefresh?: () => Promise<void> | void;
  servers: TargetServer[];
  warmups: WarmupStatus[];
}

export function TargetServerWorkspace({ observations, onCreate, onOpenServer, onRefresh, servers, warmups }: TargetServerWorkspaceProps) {
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const createServer = async (input: TargetServerInput) => {
    if (!onCreate) return;
    setBusy(true);
    setError(null);
    try {
      await onCreate(input);
      setCreating(false);
      await onRefresh?.();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'target_server_operation_failed');
      await onRefresh?.();
    } finally {
      setBusy(false);
    }
  };

  return <div className="target-server-workspace"><div className="target-server-workspace__heading"><span>TARGET SERVER REGISTRY</span>{onCreate ? <SpecularButton size="sm" onClick={() => { setCreating(true); setError(null); }}>新增服务器</SpecularButton> : null}</div>{creating ? <TargetServerForm busy={busy} error={error} onCancel={() => { setCreating(false); setError(null); }} onSubmit={input => void createServer(input)} /> : null}<TargetServerTable observations={observations} onOpenServer={onOpenServer} servers={servers} warmups={warmups} /></div>;
}
