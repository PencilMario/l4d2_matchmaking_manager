import type { TargetServer, TargetServerObservation, WarmupStatus } from '../../api/models';
import { TargetServerRow } from './TargetServerRow';
import { buildTargetServerRows } from './server-view-model';

interface TargetServerTableProps {
  observations: TargetServerObservation[];
  onOpenServer?: (server: TargetServer) => void;
  servers: TargetServer[];
  warmups: WarmupStatus[];
}

export function TargetServerTable({ observations, onOpenServer, servers, warmups }: TargetServerTableProps) {
  const rows = buildTargetServerRows(servers, observations, warmups);
  if (rows.length === 0) {
    return <p className="workspace-empty">尚未配置 Target Server。</p>;
  }

  return (
    <section aria-label="Target Server 资源表" className="target-server-table">
      <div aria-hidden="true" className="target-server-table__header">
        <span>服务器 / A2S</span><span>人数</span><span>观测</span><span>调度</span><span />
      </div>
      <div className="target-server-table__rows">
        {rows.map(row => <TargetServerRow key={row.server.id} onOpen={() => onOpenServer?.(row.server)} row={row} />)}
      </div>
    </section>
  );
}
