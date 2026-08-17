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
