# 单主机暖服核心控制器：计划检查点

## TodoCheckpointDraft

- 已完成：需求澄清、术语、基线读取、架构规格、原子任务、实施计划，以及 Task 1-3 的共享 contracts、C1-C14 metadata 和单账号 Steam session actor。
- 当前工作项：Task 4，将 Agent HTTP 接入 actor 并提供幂等大厅操作与任意 lobby 查询。
- 尚未开始：Task 4 至 Task 12；没有数据库迁移、Docker Compose 或新的运行时 API 已交付。
- 下一步：先为 Agent 创建操作的 202/200 幂等语义及 active-state lobby 读取写红灯端点测试。

## EvidenceBundleDraft

- `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj`：基线为 12/12 通过（规划前记录）。
- `dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj`：基线为 7/7 通过（规划前记录）。
- `git diff --cached --check`：计划提交前无空白错误。
- 计划/规格标记检查：10 个关键要求标记均存在，包括 C14、120/30 秒、36 并发、共享库、AppID 550、下载地区、Actor 健康快照和共享库维护锁。
- 提交：`8b59d58 docs(core): 固化暖服控制器实施计划`。
- Task 1 RED：`LobbySnapshotTests` 因缺少 `LobbySnapshot` 和 `LobbyMemberSnapshot` 编译失败。
- Task 1 GREEN：contracts 测试 1/1 通过；`dotnet build L4d2MatchmakingManager.sln --warnaserror` 为 0 警告、0 错误；Agent、Protocol、Probe 回归分别为 12/12、7/7、3/3。
- Task 1 兼容修正：contracts 多目标 `net8.0;net10.0`，使既有 `net8.0` Probe 可引用相同协议；solution 显式使用 `.sln` 与 `--in-root`，避免 .NET 10 默认 `.slnx` 和同名 solution-folder 冲突。
- Task 2 RED：`CampaignProfileTests` 因缺少 `CampaignProfile` 编译失败。
- Task 2 GREEN：3 个 campaign tests 通过；完整 protocol、Probe、Agent 与 contracts 回归分别为 10/10、3/3、12/12、1/1；统一 solution 构建为 0 警告、0 错误。
- Task 3 RED：actor test 因缺少 `SteamSessionActor`、runtime 与 campaign selector 契约编译失败。
- Task 3 GREEN：actor 假 runtime 在持有操作时并发提交 health/read，最大底层调用数为 1；runtime 在同一 actor 线程加载 Steam DLL、Manual Dispatch、创建/读取/离开大厅，并用 metadata index API 查询任意大厅。完整回归为 Probe 4/4、Protocol 10/10、Agent 12/12、contracts 1/1；solution 构建为 0 警告、0 错误。

## DriftCheckDraft

- 范围：仍为单 Docker 主机的 Core Controller 与受管 Agent；没有扩展至 Web UI、跨主机编排或 RCON。
- 兼容：现有 health 路由、Probe CLI 与 standalone Compose 保留；Core 业务路径迁移到 Agent HTTP 是计划中的 retirement track。
- 运行时权威：后续调度只可依据实时 Agent 与 A2S 读取，数据库仅保存配置、关联、审计与租约。
- 决定：`continue`，Probe CLI 保留其 native ABI wrapper 作为诊断兼容层；SteamNativeRuntime 复用它而不再引入第二个 Steam API owner。下一切片仅增加 Agent 到 actor 的内部 HTTP 映射。

## Risk / Unknown

- `AutoUpdateBehavior=1` 可阻止 AppID 550 在不启动 L4D2 时的后台更新；Agent 不启动游戏。Steam 客户端自身更新的跨版本配置尚无本机可验证证据，必须在 Ubuntu 真机验收中单独确认，不能把它表述为已经禁用。
- Steam 下载地区写入的精确 VDF 位置也需要真机验收；凭据隔离、共享库路径与 AppID 550 策略已有自动化契约覆盖计划。
