# 基线读取集

## 代码与契约

- `src/L4d2MatchmakingCore/A2s/TargetServerObservationCollector.cs`：A2S 观测的生产者。
- `src/L4d2MatchmakingCore/A2s/TargetServerObservationStore.cs`：按目标服务器保存最新快照的单例存储。
- `src/L4d2MatchmakingCore/A2s/TargetServerObservation.cs`：快照状态契约，当前状态包括 `online` 和 `unavailable`。
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`：暖服计划的 canonical owner；`PlanStartAsync` 被普通启动、续暖服和重建路径共用。
- `src/L4d2MatchmakingCore/Program.cs`：注册 `TargetServerObservationStore` 为 singleton，且调度器和观测采集器运行在同一应用进程。
- `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`：已有“调度器自身 A2S 查询失败时跳过目标”的测试，但没有覆盖已存在的 `unavailable` 快照。

## 当前基线

现有测试 `TickSkipsA2sUnavailableHigherPriorityTargetAndStartsNextTarget` 通过让调度器使用的 fake A2S 客户端抛错来验证跳过逻辑。它无法发现观测采集器先写入 `unavailable`、随后调度器使用另一条 A2S 查询路径继续计划的问题。

## 兼容边界

- 没有观测快照时，保持现有直接 A2S 探测行为。
- `online` 快照不替代现有玩家数查询，仍由调度器的直接查询决定是否达到暖服阈值。
- 已经存在的 active/uncertain 尝试仍按现有恢复和观察逻辑处理。
