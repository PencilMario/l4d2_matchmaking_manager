# Steam 大厅自动创建与暖服编排

本文定义核心控制服务创建 L4D2 Steam lobby 的自动化流程。它覆盖不要求服务器保留 lobby 的 URI 重入路径，以及要求 reservation 的真实握手路径。

当前 Agent 的 HTTP 合约仍只有只读健康检查，见 [steam-lobby-agent-api.md](steam-lobby-agent-api.md)。本文中的 Probe 启动是核心管理面在受控 Agent 主机上执行的**临时编排适配层**，不是可向第三方开放的 HTTP 接口；后续受管创建接口应保持相同的输入、状态机和判定规则。

## 边界和不变量

- 只有核心管理面可以创建、保持或离开大厅。对外 API 仅能按 lobby ID 查询只读 Steam lobby 信息，不能暴露创建、reservation、Agent 状态、任务状态或服务器状态。
- 一个 Steam 账号同一时刻只能运行一个会改变 lobby 的 Probe。核心必须按 `agentId` 加互斥锁，锁覆盖预检、创建、保持和清理的整个生命周期。
- 每个账号使用独立的 Steam 登录卷；所有账号只读写同一受控的 Steam library 挂载。共享 library 的下载、更新、校验和卸载必须串行。
- Steam Desktop、Probe 和 Steam API 均在 Agent 容器的 `default` 用户下运行。不要以 root 运行 Probe。
- 所有运行时状态都以实时查询为准，不能作为数据库中状态属性的权威来源：Agent 就绪性、lobby owner/member/metadata、服务器 `reserved` 状态和 lease 均应在需要时重新读取。数据库可保存目标服务器的静态配置及管理审计记录。
- Agent 主机 HTTP 端口只绑定回环地址。核心跨主机访问时通过带主机指纹校验的专用 SSH 隧道；不得把 Agent 或 Probe 直接暴露到公网。

## 创建请求

核心接收的创建意图至少包含：

| 字段 | 说明 |
| --- | --- |
| `agentId` | 持有 Steam 登录会话的子 Agent。 |
| `endpoint` | 目标服务器 IPv4 地址及游戏端口，格式 `ip:port`。 |
| `visibility` | `private`、`friends`、`public` 或 `invisible`。 |
| `requiresReservation` | 目标服务器是否要求 reservation。 |
| `requestedLobbyLifetimeSeconds` | Probe 保持 lobby 的时长；暖服默认请求 `120` 秒。 |
| `serverStatusVerifier` | 仅 reservation 路径需要。核心侧受保护的服务器控制台/RCON `status` 查询能力，不得把密码传给 Agent 或写进日志。 |

目标服务器的 `requiresReservation` 是静态管理配置。实际 reservation、lobby 成员和 Agent 可用性是观察值，读取时重新查询。

## 统一预检

1. 核心取得该 `agentId` 的独占锁。
2. 通过仅 Agent 主机可见的 `/v1/probe/status` 做实时预检。只有 HTTP `200`、`ready=true`、`checks.appId=550` 和 `checks.loggedOn="ok"` 时才可开始。
3. 校验 `endpoint` 为 IPv4 和有效端口，并设置进程启动、事件等待和总生命周期超时。
4. 创建一个仅内存中的执行上下文，保存 `agentId`、端点、请求时长、进程句柄和从 stdout 解析出的 lobby ID。不要先把“创建中”“已预留”等状态写入业务数据库。

在实际受管接口完成前，主机侧适配器可等价执行以下命令形状。容器名、二进制和 API 库路径应来自部署配置，不能由外部调用者传入。

```sh
docker exec --user default <agent-container> \
  /opt/steam-lobby-probe/SteamLobbyProbe \
  /mnt/steam-library/agent-runtime/steamrt64/libsteam_api.so \
  <mode> <endpoint> <visibility> 120
```

适配器只能将结构化结果回传给核心，不能把 Probe 原始 stdout、Steam 登录路径或任何服务器密钥转发到对外 API。

## 不要求 Reservation

这条路径对应 `research/SteamLobbyProbe/README.md` 的“原 owner 离开后的 URI 重入”验证。目标服务器不要求 lobby cookie；创建者离开后，已进入服务器的玩家仍保持游戏连接，旧 URI 也可以用于再次加入。

1. 以 `server` 模式启动 Probe：`<mode>` 为 `server`，并传入 `endpoint`、`visibility`、`requestedLobbyLifetimeSeconds` 和游戏状态 `game`。
2. 等待 `LobbyCreated result=1 lobby_id=<nonzero>`、`SetLobbyGameServer` 成功、`JoinURI=steam://joinlobby/550/...` 和 `Keepalive begin`。只有这些事件完整出现才向管理面返回 URI。
3. 核心将 lobby ID 和 URI 作为本次操作的结果交给受权管理调用方。第三方只读接口不返回这些自动化任务结果。
4. 进程在请求时长结束后输出 `Keepalive complete`，随后清理时输出 `LeaveLobby complete`。若玩家已通过 URI 入服，owner 离开不应被解释为玩家掉线；需要时以目标服务器的实时玩家查询验证。

当前 `server` 模式仍使用实验阶段的 legacy metadata。它尚未满足“真实 reservation lobby 的只读检查”一节所列 metadata 默认值，因此在把本路径作为正式自动化能力前，应先将其 metadata profile 统一为该真实样本；不要在核心层复制或手工拼接 metadata。

## 需要 Reservation

这条路径使用 `server-reserved`，复现真实的 `challenge -> reservation` UDP 握手。Probe 创建的 lobby CSteamID 就是 reservation cookie；它同时写入 `RealSessionSettings.CreateLobbyMetadata()` 的默认 metadata，并使用 551 字节真实 game settings 发送 reservation 请求。

1. 以 `server-reserved` 模式启动 Probe：`<mode>` 为 `server-reserved`，其余参数与上例相同。
2. 等待 `LobbyCreated result=1 lobby_id=<nonzero>`，将十进制 `lobby_id` 解析为无符号 64 位值，作为预期 reservation cookie。
3. 等待 `ReservationComplete lobby_id=<same-id> exit_code=<code>`。只接受 `0` 或 `8`；任何其他值都表示创建失败并进入清理。`0` 是收到且接受 reservation 响应，`8` 只表示 reservation 响应超时、发包已完成，必须继续做服务器侧验证，不能直接判失败。
4. 同时要求 `LobbyId=<same-id>`、`JoinURI=steam://joinlobby/550/...`、`verification=server_status_required` 和 `Keepalive begin`。任一缺失都应失败并终止 Probe。
5. 核心通过受保护的服务器状态查询执行 `status`。响应中的 `reserved <cookie>` 必须等于 lobby ID 的十六进制无前导零、小写表示。例如 lobby ID `0x018600004AADAF74` 对应 `reserved 18600004aadaf74`。
6. 第一次确认后继续保持 Probe，并在不早于首次确认 60 秒后再次查询 `status`。两次均匹配同一 cookie 才可报告“已保留至少 60 秒”。请求 120 秒是目标保持时间，不是服务器 lease 的承诺；无人入服时服务器可在接近两分钟或更早时解除 reservation。
7. 将 URI 交给受权管理调用方或受控入服分发器。完成后自然等待 `Keepalive complete` 和 `LeaveLobby complete`，或在取消/超时时终止进程并确认清理。

服务器控制台/RCON 是 reservation 成功的权威来源。不要依据普通 Steam metadata、`GetLobbyGameServer`、UDP response 是否及时返回、或 `server:reservationid` 字段单独判定已保留。

## 事件与状态机

核心只解析下列稳定的 key-value 事件，不以行号、日志语言或原始二进制内容作为协议：

| 事件 | 含义与动作 |
| --- | --- |
| `LobbyCreated` | 要求 `result=1` 和非零 `lobby_id`，否则失败。 |
| `ReservationComplete` | reservation 模式必需；只接受 `exit_code=0` 或 `8`，随后由服务器 `status` 判定。 |
| `LobbyId` | 必须等于先前的 `LobbyCreated.lobby_id`。 |
| `JoinURI` | 返回给管理面使用的 Steam lobby URI。 |
| `Keepalive begin` | 表示 lobby 已开始保持；未出现不能对外声称创建完成。 |
| `Keepalive complete` | 请求生命周期正常结束。 |
| `LeaveLobby complete` | Probe 已请求离开大厅；正常或异常收尾都应记录。 |

```text
preflight -> create lobby -> metadata -> (reservation handshake) -> keepalive
    |              |              |                 |                 |
  failed         failed         failed       status mismatch       time/cancel
    |              |              |                 |                 |
    +--------------+--------------+-----------------+-----------------+
                                   |
                              leave lobby
```

`server-reserved` 当前在首次 challenge 接收超时时可能直接异常退出，因而没有 `ReservationComplete` 或 `Keepalive begin`。这属于失败，不应按照 `exit_code=8` 处理；`8` 只适用于已发送 reservation request 后等待 response 超时的情况。

## 失败、重试和清理

- 预检失败：不启动 Probe。对短暂 Steam 回调问题采用有限指数退避；登录、AppID、库加载或部署错误需要人工恢复后才可重新调度。
- `LobbyCreated result=25`：Steam 对该账号创建 lobby 限流。结束当前任务、释放账号锁并进入冷却，不要并发或紧密循环重试。
- reservation challenge 或服务器状态查询失败：立即失败本次任务，结束 Probe 并等待 `LeaveLobby complete`；若进程已失联，重做 Agent 健康检查后再允许新任务。
- 首次或 60 秒复核的 `status` 未显示预期 cookie：判定 reservation 失败，停止 URI 分发并清理；不要将另一个 lobby 的 cookie 视为成功。
- 生命周期超时、核心取消或控制通道断开：由主机适配器终止子进程，随后检查其已退出并释放账号锁。不能依赖进程强杀后 Steam 会立即释放所有 transient 状态。
- 每次失败或完成后，核心按需重新调用 Agent health 和只读 lobby 查询；它们用于当前观测，不能读取数据库中的旧状态来替代。

## 后续受管接口

稳定的 Agent 控制接口应由核心管理面认证后调用，并以一次性创建请求和受控任务句柄实现上述状态机。它不应把 `docker exec`、容器名、Steam API 库路径、Probe 原始输出或服务器密钥暴露给调用方。迁移到该接口后，本文件中的事件判定、单账号串行化、reservation 双次 `status` 验证和运行时实时查询要求保持不变。
