# Task Intent

## Requested outcome

停止单个 Warm-up Agent 时，先退出该节点关联的当前暖服任务，再停止节点容器；若
任一任务无法确认退出，保留任务并返回冲突，允许后续重试。

## Confirmed behavior

- 使用现有 `WarmupAttemptDrainService.DrainAgentAsync` 排出 `active`/`uncertain`
  任务。
- 排出成功后才关闭 VNC、停止容器并将节点写为 `stopped`。
- 排出失败返回 `409 "warmup_agent_stop_drain_failed"`，容器不停止，任务保留，
  节点按既有失败隔离语义处理。
- 隔离节点再次调用停止接口时重试任务排出；确认成功后才停止容器。
- 节点不存在和已停止节点的既有幂等行为保持不变。

## Non-goals

- 不修改 Warm-up Agent 内部 stop API、调度选择规则、任务数据模型或前端交互。
- 不扩大 `DrainAgentAsync` 当前任务状态范围，不改变重建和全局排出流程。
