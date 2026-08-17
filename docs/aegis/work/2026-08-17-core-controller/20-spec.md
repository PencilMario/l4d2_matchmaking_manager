# L4D2 单主机暖服核心控制器设计

## 目标

交付一个可通过 Docker Compose 部署的单主机核心控制器，用于维护 L4D2 目标服务器和 Steam 暖服机，按配置的规则持续创建、持有和离开大厅，并提供受认证的管理 API 与按 lobby ID 的只读查询 API。

本次同时扩展 `L4d2LobbyAgent` 为仅供核心在 Docker 内部网络调用的受管控制服务。核心不再以 `docker exec` 调用研究工具作为业务协议。

## 已确认的约束

- 首版仅管理与核心位于同一 Docker 主机的 Agent 容器，不实现跨主机 Docker、SSH 隧道或消息队列。
- 管理能力以 REST API 提供，不实现 Web UI。
- Agent 数量可达数十至数百；每个 Agent 对应一个独立 Steam 账号和独立的持久化 Steam 数据卷。
- 对外与管理 API 均使用可配置 Bearer Token；Agent 控制 API 不发布宿主机端口。
- 核心可引入成熟依赖以处理 PostgreSQL、Docker Engine API、认证与 OpenAPI。
- Agent 在每次创建大厅时自行随机选择一份完整的官方战役档案；核心不拼接或覆盖单个地图 metadata 字段。

## 架构

```text
Authenticated REST clients
          |
          v
Core Controller (ASP.NET Core)
  | configuration API / lobby query API
  | scheduler / A2S observer / Docker provisioner
  v
PostgreSQL <---- audit, configuration, operation correlation, leases
  |
  +---- Docker Engine API ----> managed Agent containers
                                   |
                                   +--> Steam Desktop + Steam API + L4D2 lobby
```

核心、PostgreSQL 和所有 Agent 使用专用 Docker 网络。核心是唯一持有 Docker socket 的容器，且只管理带有本项目受控标签的容器、网络和卷。所有 Agent 将同一个受控 `Shared Game Library` bind mount 到相同容器路径，其中包含 L4D2 游戏本体、Steam runtime、Steam API 及运行所需依赖；该库不保存任何登录凭据。每个 Agent 的 `Account Data Volume` 独立，只保存 Steam 登录、Steam Guard、userdata 和账号级客户端配置。noVNC 仅在创建或重建 Agent 时按需映射至 `127.0.0.1` 的独立宿主机端口，供管理员经本机或 SSH 隧道完成 Steam 登录。

PostgreSQL 保存静态配置、操作 ID、状态转换审计和调度租约，但不作为 Steam、lobby、A2S 或 Agent 就绪状态的权威来源。每次调度判断、查询和恢复均重新观测运行时。

每个 Agent 进程内有一个持久的 **Steam session actor**，是该账号唯一调用 Steam API 的执行者。它在同一 Manual Dispatch 回调循环中保持大厅、采集 owner/成员/metadata 快照，并处理健康检查和任意 lobby 的只读查询命令。HTTP 请求只向 actor 投递受控命令；不得在活动 lobby 保持期间另起第二个 Steam API helper 进程。

## 配置模型

### Target Server

目标服务器有稳定 ID、原始主机名或 IPv4 地址、端口、启用状态与下列配置：

| 字段 | 默认值 | 语义 |
| --- | --- | --- |
| `endpoint` | 必填，端口缺省 `27015` | 接受 `hostname[:port]` 或 IPv4；保留主机名，A2S 查询时解析。 |
| `requiresReservation` | 必填 | 为 true 时该服务器最多一个活动暖服操作。 |
| `priority` | `0` | 任意整数，数值越高越优先，可为负数。 |
| `maxConcurrentWarmups` | `36` | 非预留服务器允许同时持有的大厅数；预留服务器固定为 1。 |
| `attemptWindowSeconds` | `720` | 一个 Agent 针对此服务器连续暖服的总时长。 |
| `playerTarget` | `6` | A2S 玩家人数达到或超过此值后停止为该服务器暖服。 |

同一优先级的服务器以持久化的稳定轮询游标选取，避免列表读取顺序改变调度结果。优先级更高的可调度服务器总是优先于更低优先级服务器。

### Warm-up Agent

Agent 有稳定 ID、显示名、Docker container ID、独立 `steam-data-<agent>` 卷、独立非敏感配置卷、可选 Steam 下载地区和生命周期状态。创建 Agent 时核心固定：

- 使用受控 Agent 镜像、网络、共享 Steam library 及项目标签；外部调用者不能传入任意镜像、volume、挂载或 Docker 参数。
- 为每个 Agent 分配唯一的 `steam-data-<agent>` 卷，正常删除、停止或重建默认不删除此卷。
- 未配置下载地区时使用 Steam 默认策略；已配置时由 Agent 在账号本地 Steam 配置中写入该地区。
- 账号本地 Steam 配置默认禁用 Shader Pre-Caching；共享库的 `appmanifest_550.acf` 固定 `AutoUpdateBehavior` 为 `1`，使 L4D2 仅在启动游戏时更新。暖服 Agent 不启动 L4D2，因此常规运行不会触发游戏自动更新；共享游戏库不保存账号凭据。

首次部署或显式维护时，核心以独占的共享库维护锁允许一个受控 Agent 下载或更新 L4D2 与依赖；其他 Agent 在维护期间不启动 Steam，也不会并发读写该库。维护结束后核心验证所需 Steam API 库存在并将 `AutoUpdateBehavior` 恢复为 `1`，再恢复 Agent 调度。日常暖服 Agent 不能自行改变共享库版本。

## 官方战役 metadata

Agent 内置 C1-C14 的不可变官方战役目录，并在每个新建大厅操作中随机选取一项。随机单元是完整的 `Campaign Profile`，因此不会产生如 C14 标题搭配 C2 任务文件的组合。

所有档案使用 `Game:chapter=1`、`Game:MissionInfo:builtin=1`、`Game:MissionInfo:Version=1`、`Game:MissionInfo:Website=http://store.steampowered.com`，以及现有会话档案中的其他非地图字段。Agent 仅按下表成组替换地图相关字段：

| 战役 | `Game:campaign` | `MissionInfo:DisplayTitle` | `MissionInfo:MissionFile` | `MissionInfo:Author` | `MissionInfo:SurvivorSet` |
| --- | --- | --- | --- | --- | --- |
| C1-C6 | `L4D2C<n>` | `#L4D360UI_CampaignName_C<n>` | `missions/campaign<n>.txt` | `Valve` | `2` |
| C7-C12 | `L4D2C<n>` | `#L4D360UI_CampaignName_C<n>` | `missions/campaign<n>.txt` | `Valve` | `1` |
| C13 | `L4D2C13` | `#L4D360UI_CampaignName_C13` | `missions/campaign13.txt` | `Valve` | `2` |
| C14 | `L4D2C14` | `#L4D360UI_CampaignName_C14` | `missions/campaign14.txt` | `Valve, NF, Roku, Jaiz, Wolphin` | `1` |

`Game:Mode=versus`、`Game:state=game`、`Members:*`、`Options:Server=official` 与 `System:*` 保持既有真实会话档案的语义。Agent 将实际选中的 campaign ID 和完整可读 metadata 回报给核心用于审计和只读查询；核心不把它作为后续创建操作的输入。

## 调度和状态机

每个 Agent 同时只能运行一个会改变大厅的操作。核心先以实时 Agent health 过滤不可调度账号，再按服务器优先级、同优先级轮询和并发约束分配空闲 Agent。A2S 人数达到目标时，该服务器不再可调度，直到后续 A2S 观测低于阈值。

### 预留服务器

预留服务器具有数据库租约和运行时复核组成的独占权。任意时刻最多一个 Agent 可执行其暖服状态机。

调度器在取得租约和创建大厅前必须先执行 `Reservation Admission Check`：A2S 观测到玩家数大于 `0` 时，直接跳过该服务器，本轮不创建大厅也不占用预留租约。此规则不同于 `playerTarget`，后者用于停止已开始的暖服；预留服务器只允许从空服状态开始。

```text
acquire lease -> create reserved lobby -> wait first external member
       ^                   |                      |
       |                   | failure              | no member for 120 s
       |                   v                      v
       +------------- leave/cleanup <--- leave then recreate same server
                                                  |
                               first external member enters
                                                  v
                              reset 30 s quiet timer on each new member
                                                  |
                        quiet / A2S target / 12 min attempt window
                                                  v
                                         leave -> release lease -> reschedule
```

创建成功必须满足 Agent 的 `LobbyCreated`、metadata 写入、reservation 执行成功和 keepalive 就绪契约。首个外部成员是相对于 Agent owner 的成员集合增量；之后每次观察到新成员加入都重置 30 秒倒计时。120 秒无人加入只会重建同一服务器的预留大厅，且仍受同一个 12 分钟尝试窗口限制。

目标服务器不提供 RCON 凭据，因此核心不执行服务器 `status` 的 reservation cookie 双重验证。Agent 已具备并经验证可正常执行 reservation；核心以其明确的 reservation 成功或失败结果、lobby 持有状态和错误码驱动清理与重试。审计必须标记该结果为 Agent 观测，而非服务器侧独立验证。

### 非预留服务器

非预留服务器允许最多 `maxConcurrentWarmups` 个 Agent 同时执行。每个 Agent 创建并持有一个标记 `Game:state=game` 的大厅，且其 metadata 由该 Agent 自行随机生成。

```text
create lobby -> wait for external members
     |                 |
     |                 +-- first/new member -> reset 30 s quiet timer
     |                                            |
     |                                      quiet expires
     |                                            v
     +------------------------------------- leave -> recreate same server

A2S target reached or 12 min attempt window reached -> leave -> reschedule
```

在未出现外部成员时，不启动 30 秒倒计时。一个 Agent 在 12 分钟窗口内因空窗离开后，始终重建同一目标服务器；窗口结束或 A2S 达标后才交回调度器。A2S 人数是停止暖服的权威来源，Steam 大厅成员仅用于判断成员增量和空窗计时。

核心以每个操作的上一轮成员集合检测进入事件，忽略拥有大厅的 Agent 自身。成员离开后再次进入视为新的进入事件并重置倒计时。由于 Steam API 的轮询本质，两个轮询周期之间发生且完全结束的加入无法被追溯，审计会记录每次实际观测时间。

## 核心与 Agent API

核心管理 API 使用 REST 和 Bearer Token，提供：

- `GET/POST/PUT/DELETE /v1/servers`：维护目标服务器和所有暖服配置。
- `GET/POST/PUT/DELETE /v1/agents`：维护 Agent 定义，创建、启动、停止、重建受控容器，并返回仅回环可访问的 noVNC 端口信息。
- `GET /v1/agents/{agentId}` 与 `GET /v1/servers/{serverId}`：返回配置、受限的运行时摘要和最近审计；不返回 Steam 凭据、Docker socket、RCON 凭据或 Agent 原始日志。
- `GET /v1/lobbies/{lobbyId}`：认证后转发到健康 Agent 提供的任意 lobby 实时读取能力，返回 metadata、owner、成员和观测时间；响应不泄露选用的 Agent 身份或容器细节。

Agent 的内部 API 只可由核心 Docker 网络访问，保留现有 `GET /healthz` 与 `GET /v1/probe/status` 的路由、响应字段和稳定失败码。新 actor 启用后，`/v1/probe/status` 由实际 actor 会话重新检查并返回新的 `observedAt`，不再在活动大厅期间启动并发 helper；部署文档必须据此更新其“每次请求启动新 Probe”的旧说明。新增接口：

- 接受带全局唯一 `operationId` 的创建/持有请求，包含目标 endpoint 与 reservation 模式；同一 `operationId` 的重试返回同一受管操作，而不新建大厅。
- 查询受管操作的 lobby ID、生命周期阶段、当前 owner/成员快照、实际 metadata 和最后观测时间。
- 结束操作并等待 Agent 确认已请求 `LeaveLobby`。
- `GET /v1/lobbies/{lobbyId}`：按任意 lobby ID 执行只读 Steam metadata、owner 与成员查询，不要求该 Agent 当前拥有、加入或正在暖服该 lobby。此命令由同一 Steam session actor 在其回调循环内执行，因此 Agent 正在持有其他大厅时仍然可用。

Agent 不接受 Docker 参数、Steam 凭据、服务器 RCON 凭据或外部指定 metadata。其单账号 Steam session actor 以顺序命令与单一回调循环覆盖健康探测、只读查询和改变大厅的操作；活动大厅持续产生可供 HTTP 读取的内存快照，而不是阻塞所有观察请求。

## 故障处理与恢复

- Agent 未就绪、Steam 未登录、Steam API 失败或容器不可达：核心不分配新操作，记录稳定失败码并按类别退避；登录与部署错误需要管理员通过 noVNC 恢复。
- 大厅创建、Agent reservation 执行、A2S 或成员读取异常：操作进入清理，核心请求 Agent 离开大厅；清理未确认时保留隔离状态，避免立即复用该 Agent 或预留服务器。
- Steam lobby 创建限流：释放当前调度资格并对该 Agent 进入冷却，禁止紧密循环重试。
- 核心重启：加载持久化操作关联后，逐一查询 Agent 当前受管操作与 lobby 运行时数据；仅在 Agent 确认无活动操作或完成清理后释放过期租约。数据库快照与真实状态冲突时以实时状态为准，并记录恢复审计。
- Agent 重启或网络失联：核心先执行健康与操作复核，再决定清理或重新调度；不得基于旧数据库状态假定 Steam 已离开大厅。

## 验证

- 单元测试：端点解析、默认值、C1-C14 目录、完整 metadata 原子替换、成员增量、所有定时器、优先级轮询、并发上限、预留独占和预留前的 A2S 空服检查。
- 集成测试：带 PostgreSQL 的核心 API、Bearer Token、持久化租约、重启恢复和核心/Agent 幂等协议。
- Docker 测试：受控 Agent 容器的标签、独立账号卷、共享游戏库挂载、自动更新禁用、内部控制端口和回环 noVNC 映射。
- 真机人工验收：共享库首次游戏下载、首次 noVNC 登录、下载地区、Shader Pre-Caching 与自动更新关闭、连续大厅创建、Agent reservation 成功结果、A2S 阈值停止及 lobby 查询。该验收需要真实已登录 Steam 账号和 L4D2 服务器，不能由模拟替代。

## 非目标

- Web 管理界面、跨主机 Agent、远程 Docker、SSH 隧道编排和消息队列。
- 公开 noVNC 或公开 Agent 控制端口。
- 将 Steam 登录数据、Steam Guard、RCON 密码、Docker socket、原始 Probe 输出或服务器密钥暴露给核心 API 调用者。
- 将数据库中保存的 Agent、大厅、A2S 或 reservation 状态视为运行时事实。
- 在没有服务器 RCON 凭据的条件下，将 Agent reservation 成功结果表述为已由服务器独立验证的 reservation lease。

## 设计输入

- 用户确认的暖服规则、默认值、单主机范围和 API 边界（2026-08-16 至 2026-08-17）。
- `CONTEXT.md`：本任务统一的领域术语。
- `docs/steam-lobby-agent-api.md`：现有 Agent health 合约与网络安全边界。
- `docs/steam-lobby-automation.md`：已验证的大厅、reservation、运行时观测和清理要求。
- `D:\Steam\steamapps\common\Left 4 Dead 2` 的 VPK 解包结果：C1-C14 任务文件、标题 token、作者和 survivor set。

## 工作草案

### TaskIntentDraft

实现单主机 Docker 核心控制服务，以 REST 管理服务器与单账号 Agent，通过受管 Agent API 执行可恢复的大厅暖服调度，并对外提供已认证的 lobby 只读查询。

### BaselineReadSetHint

当前 Agent 的健康 API、部署 Compose、Steam 大厅自动化契约、真实 metadata 研究证据和本机 VPK 中 C1-C14 任务文件均是实施前必须遵守的基线。

### ImpactStatementDraft

新增核心、数据库、部署与调度层，并扩展 Agent 的控制契约；不改变现有 health 路由语义。Steam 和服务器运行时状态维持实时读取的权威边界，公开 API 只读且不含凭据。

## 2026-08-17：登录后 VNC 回收

### 目标

一个已配置 Steam 登录的受管 Agent 重建后，必须继续以 `-silent -no-browser`
运行 Steam，同时不再启动基础镜像的 `x11vnc` 服务。首次登录和显式重新认证仍必须
保留仅回环可达的 noVNC。

### 设计

容器初始化脚本 `deploy/steam-lobby-agent/91-enable-steam-supervisor.sh` 是 Steam
登录状态到启动进程策略的唯一 owner。它读取账号私有
`config/loginusers.vdf` 中的 `MostRecent=1` 或 `l4d2-agent-ready` 标记，并在
supervisor 启动前同时写入 Steam 命令和 VNC 的 `autostart` 值。标记只由 Agent 在
Steam actor 和 Steam Desktop 同时健康时写入，因此不会将未登录账号误判为已登录：

- `STEAM_LOGIN_UI_MODE=always`：`-vgui -no-browser`，启动 VNC，供人工登录使用。
- `STEAM_LOGIN_UI_MODE=auto` 且没有最近登录：`-vgui -no-browser`，启动 VNC，维持首次
  配置流程。
- `STEAM_LOGIN_UI_MODE=auto` 且有最近登录或就绪标记，或 `never`：`-silent -no-browser`，
  禁用 VNC supervisor，避免启动 `x11vnc`。

基础镜像的 `90-configure_vnc.sh` 仍负责其通用 VNC 配置；本脚本仅在它之后覆盖
`vnc.ini` 的最终自启动决定。Core 仍保留现有的 `WEB_UI_MODE=vnc`、8083 端口映射、
Agent API、卷名、账号凭据隔离和 noVNC 端口分配契约。已登录的 Agent 因而不消耗 VNC
进程内存，但其保留的回环端口在无 VNC 服务时不会响应；需要交互登录时，将 UI 模式设为
`always` 后重建。

### 边界与非目标

本切片不修改 Steam actor、Core API、数据库、Docker 网络模型、共享游戏库或 Steam
客户端 bootstrap 更新策略。Steam 自身可能保留的 `steamwebhelper` 子进程不属于 VNC
服务，不能在不影响 Steam 会话的前提下于本切片中强行终止。

### 验收

自动化契约必须覆盖三种模式及其 VNC `autostart` 状态。远端验收使用已登录的账号在
`auto` 模式重建 Agent，确认 actor 连续健康且容器进程列表不含 `x11vnc`；`always` 模式
仍保留 VNC 登录恢复能力。现有 8080 内网与 8083 回环绑定必须不变。
