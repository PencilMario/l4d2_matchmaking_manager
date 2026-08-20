# Task Intent

## Requested outcome

在管理前端的全局设置中增加“全局启用暖服和调度”开关。关闭时持久化暂停状态，停止并清空全部 `active`、`uncertain` 与 `restart_pending` 暖服任务；暖服节点容器保持运行。重新开启后从空任务状态恢复调度，不能恢复已清空的旧任务。

## Confirmed product behavior

- 关闭开关前弹出二次确认。
- 关闭成功后所有可确认停止的暖服任务变为 `completed`，关联 reservation lease 清除。
- 停止操作无法确认时保持禁用并返回冲突，保留未确认任务以供重试。
- 不调用 Docker 容器 stop；被清空任务使用现有 Steam 重启流程恢复 agent 可调度状态。
- 开关状态保存到 `CoreSettings`，控制服务重启后仍保持禁用。

## Scope

- Core：全局设置字段、迁移、认证读写接口、全局任务排空、调度暂停与恢复。
- Frontend：旧部署入口 `GlobalSettingsView`、新版 `SettingsWorkspace`、两套 API client 与工作台 wiring。
- Tests/docs：后端排空/调度/HTTP 契约测试，前端组件/API 测试，公开 API 文档。

## Non-goals

- 不停止、删除或重建 Warm-up Agent 容器。
- 不修改单个 Target Server 的 `enabled` 状态或其现有 drain 语义。
- 不恢复关闭前已经清空的 Warmup Attempt。
- 不改变五秒轮询、Bearer 鉴权、旧 `/v1/settings` PUT 的兼容行为。

## Risk hints

- 设置写入与后台调度可能并发，必须用 Core 进程内共享 gate 串行化切换和 scheduler tick。
- 关闭操作是跨 agent 的远程 stop，部分失败时不能把任务误标记为 completed。
- 已有 `WarmupSchedulerServiceTests.cs` 有用户未提交的断言改动，不能覆盖或回滚。
