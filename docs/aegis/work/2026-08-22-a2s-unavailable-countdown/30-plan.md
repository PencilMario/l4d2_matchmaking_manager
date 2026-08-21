# A2S 不可用倒计时边界 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让活动暖服尝试在 Warm-up Attempt Window 结束前忽略 A2S 故障，结束后才释放并临时隔离不可用目标。

**Architecture:** `WarmupSchedulerService.TickCoreAsync` 已是活动 attempt 的 canonical owner，并已计算目标的倒计时截止点。A2S 失败分支按该截止点分为“本轮跳过但保留活动状态”和“调用既有释放流程”两条路径；新的计划创建仍由 `PlanStartAsync` 的即时 A2S 检查负责跳过不可用目标。

**Tech Stack:** .NET 10, C#, EF Core InMemory tests, MSTest, Docker Compose。

**Baseline / Authority Refs:** `CONTEXT.md` 中的 Warm-up Attempt Window 定义；`docs/aegis/work/2026-08-21-a2s-unavailable-warmup/30-plan.md`；`src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`；`src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`。

**Compatibility Boundary:** 不改变 A2S 采集器、API、数据库结构或新计划的跳过规则；倒计时结束后的释放、停止失败隔离、租约清理和恢复后只创建新 operation 的既有语义保持不变。

**Verification:** 先新增“窗口内保留”和“窗口结束后释放”测试并确认旧实现失败；实现最小分支后运行 Core 测试、完整解决方案测试、Release 构建、`git diff --check`，再部署并检查 Core `/healthz`、容器状态和数据库 attempt 状态。

---

### Task 1: 新增 A2S 故障倒计时边界回归

**Files:**
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** 当前 A2S 失败分支无条件释放活动 attempt；测试必须锁定用户确认的时间边界，避免以后再次提前释放或在到期后继续保留。

**Impact / Compatibility:** 测试使用现有 `FakeA2s`、`FakeAgents` 和 InMemory DbContext；不改变 fake 契约或生产 API。

**Repair Track:** 以活动 attempt 的 `StartedAt + AttemptWindowSeconds` 作为目标倒计时，覆盖窗口内不停止、不完成、不删除 lease 与窗口到期释放。

**Retirement Track:** 旧的即时 A2S 失败测试继续覆盖到期分支（把 StartedAt 设为已过期），并保留新计划跳过不可用目标的测试；不删除这些不同层次的保护。

**Verification:**

- [ ] **Step 1: Write failing tests**

新增并调整 `WarmupSchedulerServiceTests` 中的边界测试：

```csharp
ActiveA2sFailureBeforeAttemptDeadlineKeepsOperationAndLease
    AttemptWindowSeconds = 60
    StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5)
    A2S failure => StopCalls == 0, attempt remains active, reservation lease remains

ActiveA2sFailureStopsOperationAndReleasesReservationLease
    AttemptWindowSeconds = 720
    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
    A2S failure => StopCalls == 1, attempt completed, reservation lease removed
```

使用短窗口和相对当前时间的 `StartedAt` 构造稳定边界；保留停止失败、多个 attempt、restart_pending 和恢复后新 operation 测试作为到期失败保护。

- [ ] **Step 2: Run only the new tests against the current implementation**

运行：

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter FullyQualifiedName~ActiveA2sFailure
```

预期：窗口内测试失败，因为旧实现会调用 `StopOperationAsync` 并完成 attempt；到期测试保持通过。

### Task 2: 按截止时间分支处理 A2S 失败

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Test: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** 将用户确认的“倒计时内忽略、到期后释放”落实到活动 attempt 的唯一调度 owner。

**Impact / Compatibility:** 仅改变 active/uncertain attempt 在 A2S 失败时的处理；目标的本轮 `unavailableTargetIds` 仍会阻止本 tick 新增暖服，后续 A2S 恢复仍只允许新 operation。

**Repair Track:** 在 DNS/A2S 查询失败时，先将目标加入 `unavailableTargetIds`；若 `DateTimeOffset.UtcNow < attemptDeadline`，直接继续下一 attempt，保留数据库和 Agent operation；否则调用既有 `ReleaseUnavailableTargetAttemptAsync`。

**Retirement Track:** 不新增第二个隔离或恢复状态；旧的无条件释放分支收缩为到期分支，现有恢复后新建 operation 的逻辑保持为唯一恢复路径。

**Verification:**

- [ ] **Step 1: Implement the minimal conditional**

在解析失败、endpoint 为空和 `GetInfoAsync` 抛异常的三个分支统一调用：

```csharp
unavailableTargetIds.Add(target.Id);
if (DateTimeOffset.UtcNow >= attemptDeadline)
    await ReleaseUnavailableTargetAttemptAsync(activeAgent, attempt, activeAttempts, cancellationToken);
continue;
```

- [ ] **Step 2: Run the new regression tests**

运行同一 `dotnet test ... --filter FullyQualifiedName~ActiveA2sFailure` 命令，预期全部通过。

- [ ] **Step 3: Run the complete Core test project**

运行：

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore
```

预期：Core 测试 0 失败；已有停止失败、多个 attempt、restart_pending 和恢复后新 operation 回归继续通过。

### Task 3: 全量验证、部署和提交

**Files:**
- Verify: `src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj`
- Verify: `src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj`
- Deploy: `/mnt/storage/l4d2-matchmaking-core/src/L4d2MatchmakingCore/`

**Why this task exists:** 确认本次修复可安全进入已运行的 Core 服务，并保留远端源码回滚点。

**Impact / Compatibility:** 只重建 Compose `core` 服务，不重启 Postgres 或 Warm-up Agent；远端本次变更的两个源码文件先备份，恢复条件为新镜像启动失败或健康检查异常。

**Repair Track:** 远端源码 SHA-256 与本地一致后，以 `docker compose --env-file ../../.env up -d --build core` 重建；检查 `/healthz`、容器状态和活动 attempt。

**Retirement Track:** 不删除旧镜像或旧备份；旧源码备份仅作为本次部署回滚边界，后续确认稳定后由维护者按部署保留策略清理。

**Verification:**

- [ ] **Step 1:** 运行 `dotnet test L4d2MatchmakingManager.sln --no-restore`。
- [ ] **Step 2:** 运行 `dotnet build src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj --no-restore --configuration Release`。
- [ ] **Step 3:** 运行 `git diff --check`，复核 staged diff。
- [ ] **Step 4:** 远端备份 `WarmupSchedulerService.cs` 和 `WarmupSchedulerServiceTests.cs`，上传并核对 SHA-256，重建 `core`。
- [ ] **Step 5:** 远端检查 `curl -fsS http://127.0.0.1:18080/healthz`、`docker ps`、最近 Core 日志和数据库 attempt/lease 汇总。
- [ ] **Step 6:** 按实际 diff 创建单行 Conventional Commit：

```powershell
git add docs/aegis/work/2026-08-22-a2s-unavailable-countdown/30-plan.md src/L4d2MatchmakingCore/Agents/AgentControlClient.cs src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs src/L4d2MatchmakingCore/tests/AgentControlClientTests.cs src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs
git commit -m "fix(scheduler): 延迟 A2S 不可用暖服释放到倒计时结束"
```
