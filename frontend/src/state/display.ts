import type { TargetServerObservation, WarmupAgent } from '../api/models';
import { CoreApiError } from '../api/core-client';

export const observationLabels: Record<TargetServerObservation['status'], string> = {
  online: '在线', unavailable: '不可用', pending: '等待首次观测',
};

export const agentStatusLabels: Record<string, string> = {
  running: '运行中', restarting: '正在恢复', stopped: '已停止', created: '已创建', quarantined: '已隔离',
};

export const warmupStateLabels: Record<string, string> = {
  active: '进行中', uncertain: '结果未确认', completed: '已完成', stopped: '已停止', failed: '失败',
};

export const warmupPhaseLabels: Record<string, string> = {
  Selecting: '选择目标', AwaitingFirstMember: '等待外部成员进入', Active: '观测服务器人数',
};

export function labelWarmupState(value: string) { return warmupStateLabels[value] ?? '未知状态'; }
export function labelWarmupPhase(value: string) { return warmupPhaseLabels[value] ?? '未知阶段'; }
export function labelAgentStatus(agent: WarmupAgent) { return agentStatusLabels[agent.status] ?? '未知状态'; }

const errors: Record<string, string> = {
  target_server_drain_failed: '相关暖服任务未能全部停止，操作尚未完成，请检查任务状态后重试。',
  global_warmup_drain_failed: '暖服和调度仍保持禁用，但有任务未能确认停止，请检查任务状态后重试。',
  global_warmup_drain_pending: '暖服和调度仍保持禁用，仍有任务未完成停止确认，请处理任务后重试。',
  invalid_target_server_endpoint: '服务器地址格式不正确，请输入主机名或 IPv4 地址加游戏端口。',
  invalid_target_server_configuration: '服务器调度配置无效，请检查各项数值。',
  invalid_rcon_password: 'RCON 密码无效。', rcon_requires_reservation: '只有需要大厅预留的服务器可以配置 RCON 密码。',
  rcon_encryption_key_not_configured: '控制服务未配置 RCON 凭据加密密钥。', core_rcon_encryption_key_invalid: '控制服务的 RCON 凭据加密配置无效。',
  invalid_warmup_agent_name: '节点名称无效。', warmup_agent_name_exists: '节点名称已存在。', no_free_novnc_port: '没有可用的 noVNC 本机端口。',
  lobby_data_unavailable: '大厅数据暂时不可用。', lobby_operation_preservation_failed: '大厅操作未能确认保存。', lobby_query_agent_unavailable: '查询节点当前不可用。',
  core_request_failed: '请求控制服务失败。',
};

export function describeError(error: unknown) {
  const code = error instanceof CoreApiError ? error.code : error instanceof Error ? error.message : 'core_request_failed';
  return { code, message: errors[code] ?? '操作未能完成，请稍后重试。' };
}

export function formatTime(value: string | null | undefined) {
  if (!value) return '--';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '--' : date.toLocaleString('zh-CN', { hour12: false });
}

export function formatRemaining(seconds: number) {
  const safe = Math.max(0, seconds);
  return `${Math.floor(safe / 60)} 分 ${safe % 60} 秒`;
}
