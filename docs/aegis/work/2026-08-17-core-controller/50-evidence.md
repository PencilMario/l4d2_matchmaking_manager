# 单主机暖服核心控制器：计划检查点

## TodoCheckpointDraft

- 已完成：需求澄清、术语、基线读取、架构规格、原子任务、实施计划，以及 Task 1-8 的共享 contracts、C1-C14 metadata、单账号 Steam session actor、Agent 内部 HTTP API、共享库配置保护、Core 认证/持久化、目标服务器配置和本机 Agent Docker 生命周期。
- 当前工作项：Task 10，实现纯暖服决策引擎的成员增量与优先级/并发选择。
- 尚未开始：Task 11 至 Task 12；没有 Core Docker Compose 或后台调度服务已交付。
- 下一步：为外部成员加入重置 30 秒计时器、reservation=1 和普通服务器并发上限写纯状态转换红灯测试。

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
- Task 4 RED：`AgentLobbyEndpointTests` 及更新后的 Probe 状态测试因缺少 `IAgentSteamSessionService` 编译失败；证明 Agent 尚无 actor 服务边界和对应路由。
- Task 4 GREEN：`POST /v1/operations` 对首次/重复 operation ID 分别返回 202/200，`GET`/`DELETE /v1/operations/{id}` 提供状态与停止语义，`GET /v1/lobbies/{id}` 可读取任意非零 Steam lobby ID 并拒绝非法输入。`/v1/probe/status` 改从同一 actor 读取健康快照且维持 200/503 与既有字段；Agent 不再注册短生命周期 Probe 进程启动器。端点/状态子集 9/9，完整 Agent 14/14，Probe 4/4，Protocol 10/10，contracts 1/1；`dotnet build L4d2MatchmakingManager.sln --warnaserror` 为 0 警告、0 错误。
- Task 4 提交：`9e3a2c5 feat(agent): 增加持久大厅控制和查询接口`。
- Task 5 RED：新的 `Test-SteamAccountConfiguration.ps1` 在现有脚本缺少 `set_l4d2_update_policy`、`AutoUpdateBehavior=1` 和 `STEAM_DOWNLOAD_REGION` 时失败。
- Task 5 GREEN：共享 `appmanifest_550.acf` fixture 保持 Steam 默认值 0；初始化脚本在实际 AppID 550 manifest 已存在时幂等写入值 1，账号私有 `config.vdf` 维持 `DisableShaderCache=1` 并可写入下载地区。Compose/Supervisor 将 `STEAM_DOWNLOAD_REGION` 传入 Agent，过时的 short-lived Probe 进程环境变量已移除。`Test-ComposeContract.ps1`、`Test-SteamAccountConfiguration.ps1` 与 Git Bash `-n` 均以 exit 0 完成。
- Task 5 提交：`6bf0c79 feat(deploy): 共享游戏库并禁用常规自动更新`。
- Task 6 RED：Core 测试因缺少 `Program`、`MatchmakingDbContext`、EF Core provider、Testcontainers 与 `ReservationLease` 编译失败。
- Task 6 GREEN：Core 从 `CORE_API_TOKEN` 或 `CORE_API_TOKEN_FILE` 读取 token，所有 `/v1/servers` 请求受固定 Bearer handler 保护；数据库包含目标服务器、Agent、尝试、审计、预留 lease 与共享库维护 lease，预留 lease 用 `TargetServerId` 做主键。首次迁移已加入，非 Testing 环境启动时执行。认证测试验证无 token/错误 token 为 401、正确 token 为 200。完整回归为 Core 1/1（PostgreSQL Testcontainers 1 项因本机 Docker daemon 不可用而 skipped）、Agent 14/14、Probe 4/4、Protocol 10/10、contracts 1/1；solution build 为 0 警告、0 错误。
- Task 6 提交：`a8950f9 feat(core): 增加认证和 PostgreSQL 持久化基础`。
- Task 7 RED：parser/API 测试因缺少 `L4d2MatchmakingCore.Servers` 模块而编译失败。
- Task 7 GREEN：受认证的 `/v1/servers` 现在提供 create/list/get/update/delete。parser 接受 hostname 或 IPv4（缺省端口 27015），拒绝 URL、IPv6、空白、嵌入凭据及非法端口；hostnames 规范化保留。默认 priority=0、normal concurrency=36、attempt window=720、player target=6，预留目标强制 effective concurrency=1。每个变更写入无运行时真值的审计记录。`TargetServer` 14/14，完整回归为 Core 15/15、PostgreSQL 1 skipped、Agent 14/14、Probe 4/4、Protocol 10/10、contracts 1/1；solution build 为 0 警告、0 错误。
- PostgreSQL 真实环境修正：首次在远端 Docker 测试中，重复租约被同一 `DbContext` 的 ChangeTracker 拒绝，未触达数据库约束；在首个保存后调用 `ChangeTracker.Clear()` 后，测试准确断言 PostgreSQL 主键冲突。
- PostgreSQL 真实证据：`100.72.137.92`（Docker Engine 29.6.1，Ubuntu 26.04）内的临时 .NET 10 SDK 容器执行 `dotnet test ... --filter OneReservationLeaseExistsPerTarget`，Testcontainers 成功启动 `postgres:17`、应用迁移并以 1/1 通过验证同一 `TargetServerId` 只能有一个 reservation lease。远端测试容器使用 `--rm`，测试数据库容器由 Testcontainers/Ryuk 清理；源码临时目录仍在 `/tmp/l4d2-core-test-20260817-1` 与 `-2`，仅含测试归档，待最终远端验收完成后删除。
- Task 8 RED：`WarmupAgentEndpointTests` 因缺少 `NoVncPort`、Agent DTO 和生命周期端点而编译失败；`AgentControlClientTests` 因缺少 Agent HTTP client 而编译失败。
- Task 8 GREEN：`/v1/agents` 受 Bearer 认证保护，提供 create/list/get/update/start/stop/recreate/delete；创建会持久化分配 noVNC loopback 端口、独立 Steam/account-config 卷名并启动容器。端口分配同时避开现有 Agent 和任意 Docker 已绑定端口，数据库以唯一索引保存分配；删除/重建从不删除账号卷。Docker runtime 对 start/stop/delete 检查受管标签，Container API 保持 8080 未发布、8083 仅映射 `127.0.0.1`。响应不返回卷名或容器 ID。`IAgentControlClient` 固定通过内部 Docker network 的 `l4d2-agent-{id}:8080` 调用 Agent；健康选择器只返回运行且实时 Ready 的 Agent。定向 Task 8 为 6/6；Core 21/21、本机 PostgreSQL 1 skipped；solution 构建 0 警告、0 错误，完整 solution 回归为 Core 21/21 + PostgreSQL 1 skipped、Agent 14/14、Probe 4/4、Protocol 10/10、contracts 1/1。
- Task 8 提交：`2f4b70d feat(core): 管理本机 Steam Agent 容器`。
- Task 9 RED：`SourceA2sClientTests` 因缺少 `L4d2MatchmakingCore.A2s` 模块而无法编译。
- Task 9 GREEN：`SourceA2sClient` 通过 IPv4 UDP 发送 A2S_INFO，收到 challenge 时精确重发一次，并仅从完整 INFO 包读取当前玩家数；取消或超时不会复用历史数。受认证的 `GET /v1/lobbies/{id}` 只会调用实时 Ready 的 Agent，公开响应不含 Agent 身份；非法 ID 为 400、无健康 Agent 或 Agent HTTP 不可达为 503。定向为 3/3；Core 24/24、本机 PostgreSQL 1 skipped，solution build 为 0 警告、0 错误。
- Task 9 提交：`7f39cd7 feat(core): 增加 A2S 观测与大厅查询代理`。
- Task 10 RED：`WarmupDecisionEngineTests` 因缺少 `Scheduling` 命名空间、attempt snapshot 和 decision engine 而无法编译。
- Task 10 部分 GREEN：纯 `WarmupDecisionEngine` 根据实时 A2S、12 分钟 deadline、预留 selecting/awaiting-first-member/active 阶段决定继续、跳过、同目标重建或释放重调度。覆盖预留服入场前 A2S>0 跳过、120 秒无成员、30 秒静默、A2S 达到 Player Target 和 deadline 到期；外部成员首次/后续加入均正确维护集合及静默倒计时，预留有效并发为 1，普通服务器按容量和 priority 选择。Core 31/31、本机 PostgreSQL 1 skipped；同优先级持久 round-robin 游标待 Task 11 的调度持久化完成。
- Task 11 部分 GREEN：`WarmupSchedulerService` 在重启时先读取 Agent operation snapshot，只有 snapshot 缺失/非 active 才释放同 operation 的 reservation lease；共享库维护锁会短路 tick。无锁时，调度器选择容量可用的高优先级目标、健康 Agent 与 IPv4 A2S 空服观测，调用幂等 Agent operation 后持久化 attempt 和 reservation lease。生产环境由每 5 秒新 scope 的后台服务驱动，Testing 环境不启动该循环。attempt 持久化 phase、lobby ready、首位外部成员、静默时刻与成员集合；活跃 lobby snapshot 达 30 秒静默后会 stop 旧 operation、结束 attempt 并释放同 operation lease。Core 35/35、本机 PostgreSQL 1 skipped；120 秒与 A2S 阈值复用同一 engine 分支，优先级同值 round-robin 仍待补充。

## DriftCheckDraft

- 范围：仍为单 Docker 主机的 Core Controller 与受管 Agent；没有扩展至 Web UI、跨主机编排或 RCON。
- 兼容：现有 health 路由、Probe CLI 与 standalone Compose 保留；Agent 的 Probe 状态路由保留 JSON 字段和 200/503 约定，数据来源已从短生命周期进程迁移为持久 actor。共享库仍仅挂载游戏内容，账号凭据仍留在独立 `steam-data` 卷。Core 的 `/v1/servers` 认证占位路由已由 TargetServerEndpoints CRUD owner 替换。
- 运行时权威：后续调度只可依据实时 Agent 与 A2S 读取，数据库仅保存配置、关联、审计与租约。
- 决定：`continue`，Probe CLI 保留其 native ABI wrapper 作为诊断兼容层；SteamNativeRuntime 复用它而不再引入第二个 Steam API owner。Core 只通过 `IAgentControlClient` 与 Agent 交互，且实时 A2S/Agent 读取将作为后续调度的唯一运行时真值；下一切片限定为纯暖服决策引擎。

## Risk / Unknown

- `AutoUpdateBehavior=1` 可阻止 AppID 550 在不启动 L4D2 时的后台更新；Agent 不启动游戏。Steam 客户端自身更新的跨版本配置尚无本机可验证证据，必须在 Ubuntu 真机验收中单独确认，不能把它表述为已经禁用。
- Steam 下载地区写入的精确 VDF 位置也需要真机验收；凭据隔离、共享库路径与 AppID 550 策略已有自动化契约覆盖计划。
- 本机未运行 Steam Desktop。`STEAM_DOWNLOAD_REGION` 在 Ubuntu Steam 客户端实际生效及 Steam 客户端自更新抑制均只能由真机验收确认；`AutoUpdateBehavior=1` 仅覆盖 AppID 550 在未启动游戏时不自动更新的策略。
- 本机 Docker daemon 不可用，故本机 PostgreSQL 测试仍显示 skipped；但远端 Docker 已提供真实 PostgreSQL 17 的 1/1 通过证据。
- Core Agent lifecycle 尚未在远端以真实受管容器执行，因为其 Compose 部署属于 Task 12；Docker 标签保护、卷隔离和 noVNC host mapping 目前由定义/API 自动化测试覆盖，真机容器创建将留待 Ubuntu 验收。

## 2026-08-17：登录后 Steam UI 精简切片

### TodoCheckpointDraft

- 已完成：Agent 启动时依据账号私有登录状态选择 Steam UI 的实现、部署契约和远程脚本分支验证。
- 当前工作项：恢复 Task 10/11 的同优先级 round-robin、调度错误隔离与完整状态转换测试；该切片未改变其工作范围。
- 下一步：在 Task 12 的真机验收中，以实际 Steam 登录账号确认 `loginusers.vdf` 状态与 silent Steam 会话可被 Agent actor 正常使用。

### EvidenceBundleDraft

- RED：`pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1 -SharedLibraryPath deploy/steam-lobby-agent/tests/fixtures/shared-library` 在新增契约后以 exit 1 失败，错误为初始化脚本缺少从持久账号登录状态选择 UI 模式的逻辑。
- GREEN：同一账户配置契约与 `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-ComposeContract.ps1` 均以 exit 0 通过；覆盖 `loginusers.vdf`、`auto|always|never`、首次 `-vgui -no-browser`、已登录 `-silent -no-browser`，以及 Compose 不再固定 `STEAM_ARGS`。
- 静态检查：`git diff --check` 以 exit 0 通过。
- 远程运行时：在 `100.72.137.92` 上，以 `l4d2-steam-lobby-agent:local` 的短生命周期 `--rm` 容器挂载当前初始化脚本。无 `loginusers.vdf` 的 `auto` 写入 `command=/usr/games/steam -vgui -no-browser`；带 `"MostRecent" "1"` 的 `auto` 和 `STEAM_LOGIN_UI_MODE=never` 写入 `command=/usr/games/steam -silent -no-browser`；`STEAM_LOGIN_UI_MODE=always` 写入 `command=/usr/games/steam -vgui -no-browser`。`bash -n` 对同一脚本以 exit 0 通过。
- Compose 解析：远端运行 `STEAM_SHARED_LIBRARY_HOST_PATH=/tmp docker compose -f /tmp/l4d2-steam-ui-compose-test-20260817.yml config` 以 exit 0 输出 `STEAM_LOGIN_UI_MODE: auto`，且保留 loopback noVNC/HTTP、共享库 bind mount 和非 GPU 设置。

### DriftCheckDraft

- 范围：仅变更 Agent 的 Steam 启动参数选择、操作员恢复开关和对应文档/契约；没有触碰 Core 调度、Agent HTTP API、账号卷边界或 noVNC 网络暴露。
- 兼容：首次启动继续使用小屏 UI；已有有效的最近登录记录后仅隐藏 Steam 窗口和浏览器进程，loopback noVNC 仍存在以便 `always` 模式重新认证。
- 决定：`continue`。`STEAM_ARGS` 固定值已从 standalone Compose 移除；启动命令的唯一 owner 是容器初始化脚本。

### Risk / Unknown

- `loginusers.vdf` 的 `MostRecent=1` 仅表示账号曾成功登录，不证明当前 OAuth/Steam Guard 凭据没有过期。凭据过期时使用 `STEAM_LOGIN_UI_MODE=always` 重新登录；真机验收仍需证明 silent 会话可完成 Agent health 和 Steam actor 初始化。

## 2026-08-17：调度公平性与重建语义切片

### TodoCheckpointDraft

- 已完成：Task 10 的同优先级持久轮询游标、预留/普通服务器容量选择和定时决策规则；Task 11 的 Agent operation 读取异常隔离，以及同目标重建与满服跳过逻辑。
- 当前工作项：处理 Agent 创建/停止请求在网络失败时的不确定操作，并补齐 Core Compose 与运维文档。
- 下一步：对 start/stop 的不确定性先写红灯测试，确保 reservation 目标在 Agent 回应缺失时仍不能被重复分配。

### EvidenceBundleDraft

- RED/GREEN：`TickPersistsRoundRobinCursorBetweenEqualPriorityTargets` 先因 `TargetServerRotationCursors` 缺失而无法编译；新增按 priority 键控的 cursor 实体、EF 映射和 `202608170004_TargetServerRotationCursor` 迁移后以 1/1 通过。游标在成功启动后更新，跨 DbContext change tracker 清除后仍使下一轮选择另一个同优先级 Target Server。
- RED/GREEN：`AgentOperationReadFailureQuarantinesAgentAndKeepsReservationLease` 与 `RecoveryOperationReadFailureQuarantinesAgentAndKeepsReservationLease` 初始均因 `HttpRequestException` 直接冒泡失败；调度和恢复现在将 Agent 标为 `quarantined`、写入无敏感信息审计，并保持 attempt/预留 lease，两个测试以 2/2 通过。
- RED/GREEN：`TickRecreatesQuietLobbyOnItsOriginalTargetBeforeHigherPriorityServers` 初始错误选择更高优先级服务器；recreate decision 现在优先使用本 tick 的原 Target Server 池。`TickRecreationPreservesTheParentAttemptWindow` 初始显示重建重置 StartedAt；替换操作现在保留父 attempt 的 StartedAt，因此 12 分钟 deadline 跨大厅重建连续生效。
- RED/GREEN：`TickSkipsFullHigherPriorityTargetAndStartsNextTarget` 初始无 operation；调度器现在逐个执行 DNS/A2S admission，玩家数达 target 或预留服非空时跳过该候选并继续同优先级/低优先级候选。
- 回归：`dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupDecisionEngineTests|WarmupSchedulerServiceTests" --no-restore` 以 16/16 通过。

### DriftCheckDraft

- 范围：仅在 Scheduler、纯选择器、PostgreSQL 迁移和相关测试中补足已有设计；没有改变外部 Core/Agent API、metadata、凭据卷或 Docker 网络边界。
- 兼容：高优先级仍优先，只有已触发 `RecreateSameTarget` 的服务器在该 tick 获得重建优先权；空服/满服判断仍以当前 A2S 结果为准。
- 决定：`continue`。Agent Start/Stop HTTP 调用的未知结果仍需在下一切片持久化为不确定操作，防止请求超时后的重复 reservation。

### Risk / Unknown

- 当前轮转游标只在 Agent Start 返回时推进；若 HTTP 请求在 Agent 已创建大厅后超时，现有实现尚未持久化该未知操作和 reservation 排他性。这是下一切片的明确修复项。

## 2026-08-17：不确定操作与 A2S 故障边界切片

### TodoCheckpointDraft

- 已完成：Agent Start/Stop 操作的不确定结果隔离，以及活动/候选 A2S 观测错误的无中断处理。
- 当前工作项：提供 Core Compose、secret-file 配置、部署/API 文档，并在远程 Docker 主机执行完整容器验收。
- 下一步：先为 Core Compose 建立静态部署契约，禁止 PostgreSQL 端口与 Agent HTTP 对外发布。

### EvidenceBundleDraft

- RED/GREEN：`StartFailureQuarantinesAgentAndPreservesReservationExclusion` 初始直接收到 Agent HTTP 异常。调度器现在于 Start 前以 `uncertain` 状态写入 operation ID 与 reservation lease，成功响应后才设置 active/推进 cursor；失败则隔离 Agent。第二个 Tick 读取仍失败时没有再次 Start，测试以 1/1 通过。
- RED/GREEN：`StopFailureQuarantinesAgentAndKeepsReservationLease` 初始直接抛出。Stop 请求失败后 attempt 保持 active、lease 不释放、Agent 标为 `quarantined`，测试以 1/1 通过。
- RED/GREEN：`ActiveA2sFailureKeepsReservationLease` 与 `TickSkipsA2sUnavailableHigherPriorityTargetAndStartsNextTarget` 初始均因 `TimeoutException` 冒泡失败。活动 attempt 的 DNS/A2S 观测失败现保持状态；候选预检查失败会继续尝试下一台，两个测试以 2/2 通过。

### DriftCheckDraft

- 范围：仅补足既有调度器对 Agent/A2S 运行时依赖的错误边界；`uncertain` 是已有 `WarmupAttempt.State` 的持久状态，不引入新的外部协议。
- 兼容：取消请求仍终止后台服务；只有成功的 Agent operation snapshot 为 active 才会把 uncertain attempt 转为 active，确认缺失/停止才释放 reservation lease。
- 决定：`continue`。未改变 HealthyAgentSelector 的单 Agent 选择接口；后续可按真实数百 Agent 负载决定是否引入有界多启动循环。

### Risk / Unknown

- Start 成功但返回非 active 状态的 Agent 失败码尚需在 Agent 真机契约中分类；当前 Agent actor 的已知成功/停止状态为 active/stopped。

## 2026-08-17：Core Compose、受管容器与 PostgreSQL 验收切片

### TodoCheckpointDraft

- 已完成：Task 11 的持久化调度、恢复、租约和不确定操作处理；Task 12 的 Core/PostgreSQL Compose、secret-file 配置、Core/Agent API 与部署文档、镜像构建契约，以及无 Steam 凭据的远端容器验收。
- 当前工作项：真实 Steam 登录、AppID 550 下载与 `libsteam_api.so` 发现后的端到端暖服验收。
- 下一步：在真实已登录 Steam 账号和目标 L4D2 服务器可用时，按 `docs/matchmaking-core-api.md` 配置共享库，完成 noVNC 登录、Agent health、reserved/standard 状态机、A2S 阈值和 lobby 查询验收。

### EvidenceBundleDraft

- RED/GREEN：Core Compose contract 依次因缺少 secret-file API token、Agent-visible `libsteam_api.so`、登录 UI 模式和 Agent image tag 而失败；`deploy/matchmaking-core/docker-compose.yml` 现在仅将 Core API 映射到 loopback、仅 Core 挂载 Docker socket、PostgreSQL 不发布端口，并以 Compose secret 读取 token。
- RED/GREEN：Agent Dockerfile contract 先发现其未复制 `L4d2Matchmaking.Contracts`，导致干净 Docker restore 无法解析 Agent/Probe 依赖；复制 contracts project/source 后 Agent 与 Probe 构建通过。
- RED/GREEN：受管 Agent 定义测试先缺少 Steam environment、FUSE、安全策略、2 GiB shm 和 `unless-stopped`；Core 现在显式创建与 standalone Agent Compose 等价的低内存/软件渲染运行配置，且 `CORE_AGENT_STEAM_LOGIN_UI_MODE` 可在重建时恢复 Steam UI。
- RED/GREEN：远端临时 Core API 创建 Agent 时 PostgreSQL 返回 `23502`，根因为初始 migration 的 audit identity 注解被写成字符串，数据库没有生成 `LobbyOperationAudits.Id`。新增 `202608170005_RepairLobbyOperationAuditIdentity` 为已有数据库添加 identity 并将 sequence 推进到现有最大 ID；`AuditRowsReceiveDatabaseGeneratedIds` 在远端 Testcontainers PostgreSQL 17 以 1/1 通过。
- 自动化：`pwsh deploy/matchmaking-core/Test-ComposeContract.ps1`、Agent Compose contract 和账号配置 contract 均 exit 0；`dotnet test L4d2MatchmakingManager.sln --no-restore` 为 46 passed、2 skipped（本机 Docker 不可用）；`dotnet build L4d2MatchmakingManager.sln --warnaserror --no-restore` 为 0 warnings、0 errors。
- 远端 Docker：`100.72.137.92` 解析最终 Compose 后，以独立 image tag、网络、端口 28080、数据库卷和空共享库启动 Core/PostgreSQL。`/healthz` 为 200，未鉴权 `/v1/servers` 为 401，Bearer 请求为 200。Core 成功创建并删除无凭据 Agent；Docker inspect 确认只有 8083 映射至 `127.0.0.1:28083`、8080 未发布，包含共享库 bind mount、独立账号卷、FUSE、AppArmor/seccomp、2 GiB shm、`unless-stopped` 和所需 Steam 环境。
- 清理：远端临时 Core、PostgreSQL、Agent、网络、数据库卷、账号卷、临时镜像、token、共享库和源码目录均已删除；验收前已存在的 `steam-lobby-agent-steam-lobby-agent-1` 保持 `Up`。

### DriftCheckDraft

- 范围：保持单 Docker 主机、Core 唯一 Docker socket owner 和 Agent 内网 HTTP 边界；没有加入 Web UI、跨主机编排、RCON 或公开 Agent 端口。
- 兼容：standalone Agent Compose 和既有 health 路由保留。Core 创建 Agent 不再遗漏 standalone 运行前提，账号卷仍在正常删除/重建时保留；新 audit migration 仅修复 PostgreSQL 自增键。
- 决定：`continue`。Core/Agent Docker 验证有直接证据；真实 Steam lobby 与服务器状态机仍必须由账号和服务器环境验收。

### Risk / Unknown

- 尚未使用真实 Steam 凭据登录 noVNC，未下载/定位实际 AppID 550 `libsteam_api.so`，因此不能声称 Steam actor、下载地区、silent 登录或 shader/update 策略已完成真实客户端验收。
- 未使用真实 L4D2 目标服务器，reserved/standard lobby 创建、A2S 玩家阈值、120/30 秒计时、恢复与任意 lobby 查询尚未完成端到端验收。

## 2026-08-17：部署审查修复切片

### TodoCheckpointDraft

- 已完成：审查中发现的 PostgreSQL identity 迁移兼容、Agent API 错误状态文档及 AppID 550 自动更新范围澄清。
- 当前工作项：以真实 Steam 账号和真实 L4D2 服务器执行 Task 12 的端到端验收。
- 下一步：完成 noVNC 登录和 AppID 550 下载后，逐项验证 Steam actor、下载地区、silent 登录、reserved/standard 调度、A2S 阈值、lobby 查询及 Core 重启恢复。

### EvidenceBundleDraft

- RED：远端临时 PostgreSQL 17 先将 `LobbyOperationAudits.Id` 人工补为 identity，再执行旧的无条件 `ADD GENERATED ... AS IDENTITY`；数据库按预期拒绝重复添加 identity。
- GREEN：`202608170005_RepairLobbyOperationAuditIdentity` 现在仅在列既非 identity、也没有既有序列默认值时才添加 identity，并始终将序列推进至最大现有 ID。远端临时 PostgreSQL 对“官方旧表”和“已人工修复表”分别插入两条审计记录，得到 `{1,2}`；临时容器已移除。
- 回归：`AuditIdentityRepairMigrationAcceptsAnAlreadyRepairedDatabase` 模拟 `004` 后的人工 identity 修复，再执行完整迁移并写入审计记录。本机 Docker 不可用，故该 Testcontainers 测试显示 skipped；测试工程成功编译，SQL 已由远端 PostgreSQL 17 直接执行验证。
- API：`CreateReturnsConflictForDuplicateName` 锁定重复名称为 `409 Conflict`。`docs/matchmaking-core-api.md` 现在说明字段错误 `400`、名称/noVNC 冲突 `409`、未知 ID `404`、Docker 生命周期异常 `500`。
- 更新策略：自动化保证范围是共享 `appmanifest_550.acf` 的 `AutoUpdateBehavior=1`，即 AppID 550 仅在游戏启动时更新；Agent 从不启动 L4D2。Steam 客户端 bootstrap 自更新没有跨版本可验证的控制开关，部署文档明确不将其表述为已禁用。
- 全量回归：`dotnet test L4d2MatchmakingManager.sln --no-restore` 为 47 passed、3 skipped（本机 Docker/Testcontainers 不可用）；三个 Compose/Steam 配置合同脚本均 exit 0；`dotnet build L4d2MatchmakingManager.sln --warnaserror --no-restore` 为 0 warnings、0 errors。

### DriftCheckDraft

- 范围：仅修复已有 PostgreSQL 迁移的历史库兼容性并校正 API/更新策略文档；没有改变暖服状态机、网络暴露、凭据卷或 Docker 权限边界。
- 兼容：已修复为 identity 或序列默认值的数据库均可继续升级；官方旧表仍取得 identity。Agent API 实现未改变，文档和回归测试现在与其实际状态码一致。
- 决定：`continue`。部署自动化与数据库兼容性已有直接证据；Task 12 仍等待真实 Steam/服务器验收。

### Risk / Unknown

- 真实 Steam 登录、AppID 550 下载与 API 库路径、下载地区、silent UI、shader/AppID 更新策略尚无客户端级证据。
- Steam 客户端自身 bootstrap 更新未被本项目禁用；这不属于已实现的 AppID 550 游戏更新策略，若业务需要必须先在目标 Steam 版本上验证稳定运维方案。
- reserved/standard lobby、A2S 阈值、120/30 秒计时、任意 lobby 查询及 Core 重启恢复仍需真实服务器验收。

## 2026-08-17：真实 Steam 登录与 VNC 回收切片

### TodoCheckpointDraft

- 已完成：真实 Steam/Steam Guard noVNC 登录、silent Agent 重建、下载地区默认值修复和登录后 VNC 回收。
- 当前工作项：等待真实目标 L4D2 服务器后执行 reserved/standard 暖服状态机验收。
- 下一步：配置目标服务器并验证 A2S、120/30 秒计时、人数阈值、lobby 查询及重启恢复。

### EvidenceBundleDraft

- 远端实测发现受管 Agent 的 `loginusers.vdf` 没有 `MostRecent` 条目，不能用文件存在性或 Steam 旧标记判断当前登录。真实 actor 已能返回 `ready=true`，因此 Agent 现在只在 actor 与 Steam Desktop 同时健康时把无敏感 `l4d2-agent-ready` 写入账号私有 Steam 配置。
- RED/GREEN：`GetAsyncPersistsTheLoginMarkerOnlyAfterTheFullHealthCheckSucceeds` 在缺少 marker 契约时以 `IAgentReadinessMarker` 未定义失败；实现后 `ProbeStatusServiceTests` 为 5/5，完整 `L4d2LobbyAgent` 测试为 15/15。账号配置契约和 Compose 契约均 exit 0。
- RED/GREEN：无下载地区的 Docker 定义测试先以预期 16 项、实际 15 项失败；Core 现在始终发送 `STEAM_DOWNLOAD_REGION=`，`DockerAgentContainerRuntimeTests` 为 3/3。这个修复消除了 supervisor 对未定义 `ENV_STEAM_DOWNLOAD_REGION` 的启动失败。
- 远端 Bash 与一次性容器覆盖 `always`、无登录的 `auto`、有 `MostRecent` 的 `auto` 与 `never`：前两种产生 VNC=true 和 `-vgui -no-browser`，后两种产生 VNC=false 和 `-silent -no-browser`。
- 远端部署：Core 镜像与 Agent 镜像重建后，首次新 Agent 启动经三次 `200` probe 写入就绪标记且仍保持 VNC=true；第二次使用同一账号卷重建后，连续三次 `200/ready=true`，`vnc.ini` 两个 autostart 条目均为 false，`x11vnc` 进程数为 0，Steam 进程仍为 1。Core 18080 与 noVNC 18083 仍只监听 `127.0.0.1`，Agent 仅发布回环 8083，8080 未发布。

### DriftCheckDraft

- 范围：只补足已登录会话的 VNC 回收和无下载地区 Agent 的容器启动契约；没有变更 Steam API、Core API、账号卷/共享库边界或暖服调度。
- 兼容：`always` 与没有登录证据的首次 `auto` 仍可 noVNC 登录；凭据失效时仍通过 `always` 重建恢复。保留回环 8083 映射，静默运行时其后端服务不启动。
- 决定：`continue`。真实登录、silent actor 与 VNC 回收已有直接证据；真实服务器状态机仍未验收。

### Risk / Unknown

- Docker 根分区在 Agent/Core 镜像构建后约剩 `3.8 GB`；`/mnt/storage` 有充足空间，但批量扩容前必须迁移 Docker data-root 或释放经确认不用的镜像。
- Steam 客户端 bootstrap 自更新仍没有已验证的跨版本禁用策略；AppID 550 的非启动时更新抑制不等同于 Steam 客户端更新禁用。
- 没有配置真实目标服务器，因此 reserved/standard 暖服、A2S 阈值、120/30 秒计时、任意 lobby 查询与 Core 重启恢复仍未完成端到端验收。
