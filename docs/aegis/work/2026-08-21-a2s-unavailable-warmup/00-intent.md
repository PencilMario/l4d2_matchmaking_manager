# A2S 不可用服务器跳过暖服

## 目标

当目标服务器的 A2S 观测快照为 `unavailable` 时，暖服调度不应为该服务器创建新的暖服尝试；调度应继续尝试同一批次中其他可用目标。

## 事实、假设与未知

- 事实：`TargetServerObservationCollector` 每 5 秒采集目标服务器，并在解析地址或 A2S 查询失败时写入 `Status = "unavailable"`。
- 事实：`WarmupSchedulerService` 当前不读取 `TargetServerObservationStore`，而是在 `PlanStartAsync` 中独立执行 A2S 查询。
- 假设：最新快照的 `unavailable` 状态足以阻止本次计划；没有快照或状态为其他值时，保留现有直接 A2S 查询作为兜底。
- 未知：远端部署环境中的采集器和调度器启动时序不属于本次代码测试范围。

## 非目标

- 不改变 A2S 采集间隔、状态接口或存储格式。
- 不主动停止已经运行中的暖服；本修复只约束新的普通、续暖服和重建计划。
