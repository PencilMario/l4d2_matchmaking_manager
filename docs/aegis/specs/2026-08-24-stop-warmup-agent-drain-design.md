# 停止暖服节点时排出相关任务设计

## 目标

停止一个 Warm-up Agent 前，先结束该节点关联的当前暖服任务。只有所有相关
Agent 操作都得到停止确认后，才停止节点容器并将节点标记为 `stopped`。

## 方案

复用现有 `WarmupAttemptDrainService.DrainAgentAsync`，由
`WarmupAgentService.StopCoreAsync` 在关闭 VNC 和停止容器前调用它。

成功路径为：

1. 查找 Warm-up Agent。
2. 排出该节点的 `active`/`uncertain` 暖服任务；成功确认的任务标记为
   `completed`，对应 reservation lease 按 operation ID 释放。
3. 关闭该节点的 VNC 会话。
4. 停止节点容器，更新节点状态为 `stopped`。

如果排出结果为 `false`，抛出 `warmup_agent_stop_drain_failed`。Endpoint 将其转换为
`409 Conflict`；节点容器不停止，未确认任务保留原状态，后续可重试停止。任务排出
过程已有的失败隔离语义保持不变。

节点不存在仍返回 `404`；没有相关任务时排出成功，停止操作保持幂等。停止已不是
`running` 的节点不重复执行排出或容器停止。

## 方案比较

- **方案 A（采用）**：停止前复用 `DrainAgentAsync`。任务停止、任务状态和租约清理
  只有一个实现，且失败时不会把容器置于不可确认状态。
- **方案 B**：在节点服务中直接查询并修改任务状态。会重复 Agent 操作停止、租约
  清理和失败隔离逻辑，容易造成任务记录与 Agent 实际状态不一致。
- **方案 C**：先停止容器，再排出任务。容器停止后可能无法访问 Agent 操作接口，
  无法安全确认任务已经退出。

## 测试与验收

- 带有一个或多个暖服任务的节点停止成功：相关 operation 均收到停止请求，任务
  变为 `completed`，对应租约释放，容器停止，接口返回 `200`。
- 任一任务停止请求失败：接口返回 `409 "warmup_agent_stop_drain_failed"`，任务
  保持未确认状态并按既有语义隔离节点，容器不会停止。
- 无任务或节点已停止：保持当前幂等行为。
- 既有重建节点排出、全局排出、Agent 生命周期和调度测试继续通过。

## 兼容边界与非目标

- 保留 `POST /v1/agents/{agentId}/stop` 路径、响应模型和鉴权方式。
- 不修改 Warm-up Agent 容器内部 API、暖服调度策略、数据库模型或前端交互。
- 不改变 `DrainAgentAsync` 对任务失败、隔离和 reservation lease 的既有处理。

## 工作草稿

### TaskIntentDraft

停止单个 Warm-up Agent 时，确保该节点的相关暖服任务已退出；排出不确定时安全失败
并允许重试。

### BaselineReadSetHint

主要基线为 `WarmupAgentService.StopCoreAsync`、`WarmupAgentEndpoints.StopAsync`、
`WarmupAttemptDrainService.DrainAgentAsync`、`WarmupAgentEndpointTests`、
`WarmupAttemptDrainServiceTests` 以及暖服节点 API 文档。

### ImpactStatementDraft

影响 Core Controller 的 Agent 生命周期入口与暖服任务排出边界。必须保持任务停止
确认后再停容器、排出失败保留任务并返回冲突、节点停止接口幂等；不涉及 Agent 容器
生命周期以外的任务调度、前端和持久化结构。
