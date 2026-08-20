# A2S 不可用服务器跳过暖服 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在暖服计划创建前阻止最新 A2S 观测为 `unavailable` 的目标服务器。

**Architecture:** `WarmupSchedulerService` 注入现有 singleton `TargetServerObservationStore`，并在共享的 `PlanStartAsync` 入口读取目标快照。只有明确的 `unavailable` 快照会短路；缺少快照或其他状态继续走当前直接 A2S 查询，因此采集器和调度器之间不会引入新的状态源或改变在线目标的玩家数判断。

**Tech Stack:** .NET 10, C#, EF Core InMemory tests, MSTest.

**Baseline / Authority Refs:** `docs/aegis/work/2026-08-21-a2s-unavailable-warmup/00-intent.md`; `10-baseline-readset.md`; `TargetServerObservationCollector`; `WarmupSchedulerService`。

**Compatibility Boundary:** 保留 `WarmupSchedulerService` 现有可选依赖的调用兼容性；没有观测时继续独立 A2S 探测；不改变 active/uncertain 尝试处理。

**Verification:** 先运行新增测试确认旧代码失败，再运行同一测试确认修复通过；随后运行 Core 测试项目、解决方案构建和 `git diff --check`。

---

### Task 1: 覆盖已有 unavailable 快照的调度回归

**Files:**
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`
- Modify: `docs/aegis/work/2026-08-21-a2s-unavailable-warmup/50-evidence.md`

**Why this task exists:** 保护采集器已报告 A2S 不可用时，调度器不能仅因自己的后续探测成功或时序不同而创建暖服。

**Impact / Compatibility:** 测试使用独立的 `TargetServerObservationStore` 注入调度器；低优先级目标仍应成为同一 tick 的暖服目标。

**Repair Track:** 根因是调度计划边界没有消费已采集的不可用状态；测试必须先证明该边界当前缺失。

**Retirement Track:** 现有 fake A2S 抛错测试继续保留，覆盖即时探测失败；新增测试覆盖共享观测状态，两者职责不同，不删除任一测试。

**Verification:**

- [ ] **Step 1: Write the failing test**

新增 `TickSkipsTargetWithUnavailableObservationAndStartsNextTarget`，创建 `TargetServerObservationStore`，为高优先级目标写入 `Status = "unavailable"`，并向构造器传入该 store；fake A2S 对两个目标都返回低玩家数。现有 `TickSkipsA2sUnavailableHigherPriorityTargetAndStartsNextTarget` 继续覆盖调度器即时 A2S 失败。

- [ ] **Step 2: Run test to verify it fails**

运行：`dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupSchedulerServiceTests.TickSkipsA2sUnavailableHigherPriorityTargetAndStartsNextTarget --no-restore`

预期：旧实现会创建高优先级目标的暖服尝试，断言低优先级目标失败。

### Task 2: 在共享计划入口消费不可用观测

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** 让普通启动、续暖服和重建路径共享同一条不可用目标拦截规则。

**Impact / Compatibility:** 新增可选的 `TargetServerObservationStore` 依赖并放在现有可选参数之后；生产 DI 会注入已注册的 singleton，现有直接构造测试保持可编译。`PlanStartAsync` 仍执行现有 A2S 查询以处理缺少或非 `unavailable` 快照的情况。

**Repair Track:** 在 active/concurrency/lease 检查之后、DNS/A2S 直接查询之前读取 `observationStore?.Get(server.Id)`；状态为 `unavailable` 时返回 `null`。

**Retirement Track:** 调度器自己的即时 A2S 查询仍是必要兜底，不因共享快照接入而删除；当所有创建路径都保证先有新鲜观测且该契约被明确后，才可单独评估移除重复探测，本任务不扩大范围。

**Verification:**

- [ ] **Step 1: Write minimal implementation**

在 `PlanStartAsync` 中加入：

```csharp
if (observationStore?.Get(server.Id)?.Status == "unavailable")
    return null;
```

- [ ] **Step 2: Run target regression**

运行上述 `dotnet test` 命令，预期新增回归通过。

- [ ] **Step 3: Run related Core coverage**

运行：`dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore`

预期：Core 测试全部通过。

### Task 3: 完成构建与工作树检查

**Files:**
- Verify: `src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj`
- Verify: `src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj`

**Why this task exists:** 确认新增依赖注入参数不会破坏编译、反射式测试 helper 或其他项目。

**Verification:**

- [ ] **Step 1:** 运行 `dotnet build L4d2MatchmakingManager.sln --no-restore`。
- [ ] **Step 2:** 运行 `git diff --check`。
- [ ] **Step 3:** 将实际命令、退出码、测试数量和剩余未知写入 `50-evidence.md`。
