# 单主机暖服核心控制器：计划检查点

## TodoCheckpointDraft

- 已完成：需求澄清、术语、基线读取、架构规格、原子任务、实施计划，以及 Task 1-8 的共享 contracts、C1-C14 metadata、单账号 Steam session actor、Agent 内部 HTTP API、共享库配置保护、Core 认证/持久化、目标服务器配置和本机 Agent Docker 生命周期。
- 当前工作项：Task 9，增加 A2S 实时观测和由健康 Agent 提供的大厅查询代理。
- 尚未开始：Task 9 至 Task 12；没有 Core Docker Compose、A2S 查询、暖服状态机或调度后台服务已交付。
- 下一步：为 Source A2S challenge 流程和匿名/无健康 Agent 的 lobby 查询路由写红灯测试。

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

## DriftCheckDraft

- 范围：仍为单 Docker 主机的 Core Controller 与受管 Agent；没有扩展至 Web UI、跨主机编排或 RCON。
- 兼容：现有 health 路由、Probe CLI 与 standalone Compose 保留；Agent 的 Probe 状态路由保留 JSON 字段和 200/503 约定，数据来源已从短生命周期进程迁移为持久 actor。共享库仍仅挂载游戏内容，账号凭据仍留在独立 `steam-data` 卷。Core 的 `/v1/servers` 认证占位路由已由 TargetServerEndpoints CRUD owner 替换。
- 运行时权威：后续调度只可依据实时 Agent 与 A2S 读取，数据库仅保存配置、关联、审计与租约。
- 决定：`continue`，Probe CLI 保留其 native ABI wrapper 作为诊断兼容层；SteamNativeRuntime 复用它而不再引入第二个 Steam API owner。Docker lifecycle 仍只使用 Core 到 Docker 和 Core 到 Agent 的独立 owner；下一切片限定为 A2S 观测与大厅查询代理。

## Risk / Unknown

- `AutoUpdateBehavior=1` 可阻止 AppID 550 在不启动 L4D2 时的后台更新；Agent 不启动游戏。Steam 客户端自身更新的跨版本配置尚无本机可验证证据，必须在 Ubuntu 真机验收中单独确认，不能把它表述为已经禁用。
- Steam 下载地区写入的精确 VDF 位置也需要真机验收；凭据隔离、共享库路径与 AppID 550 策略已有自动化契约覆盖计划。
- 本机未运行 Steam Desktop。`STEAM_DOWNLOAD_REGION` 在 Ubuntu Steam 客户端实际生效及 Steam 客户端自更新抑制均只能由真机验收确认；`AutoUpdateBehavior=1` 仅覆盖 AppID 550 在未启动游戏时不自动更新的策略。
- 本机 Docker daemon 不可用，故本机 PostgreSQL 测试仍显示 skipped；但远端 Docker 已提供真实 PostgreSQL 17 的 1/1 通过证据。
- Core Agent lifecycle 尚未在远端以真实受管容器执行，因为其 Compose 部署属于 Task 12；Docker 标签保护、卷隔离和 noVNC host mapping 目前由定义/API 自动化测试覆盖，真机容器创建将留待 Ubuntu 验收。
