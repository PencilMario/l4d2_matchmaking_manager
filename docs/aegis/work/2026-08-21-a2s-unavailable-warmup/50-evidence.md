# 验证证据

## 红绿循环

- 红灯命令：`dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupSchedulerServiceTests.TickSkipsTargetWithUnavailableObservationAndStartsNextTarget --no-restore`
- 红灯结果：退出码 `1`；`1` 个失败、`0` 个通过。旧实现为高优先级且已有 `unavailable` 快照的目标创建了暖服尝试，断言实际目标为 `unavailableTarget` 而非 `nextTarget`。
- 修复：`WarmupSchedulerService.PlanStartAsync` 在 active/concurrency/lease 检查后、DNS/A2S 查询前读取可选 `TargetServerObservationStore`；明确状态为 `unavailable` 时返回 `null`。
- 绿灯命令：同上。
- 绿灯结果：退出码 `0`；`1` 个通过、`0` 个失败。

## 相关测试与构建

- Core 测试：`dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore`
  - 退出码 `0`；`138` 个通过、`0` 个失败、`3` 个跳过，共 `141` 个测试。
- 解决方案构建：`dotnet build L4d2MatchmakingManager.sln --no-restore`
  - 退出码 `0`；`0` 个警告、`0` 个错误。
- 工作树检查：`git diff --check`
  - 退出码 `0`，未发现空白错误。

## 兼容性与剩余未知

- 保留 `WarmupSchedulerService` 现有可选依赖顺序；测试中的反射构造 helper 已补上新增末尾 `null` 参数。
- 没有快照或快照状态不是 `unavailable` 时，调度器仍执行原有直接 DNS/A2S 查询；现有即时 A2S 抛错测试未删除且 Core 全量通过。
- 本次未验证远端部署中的采集器/调度器启动时序，以及真实网络 A2S 行为；这些属于计划明确的运行环境未知。
