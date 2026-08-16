# 证据与检查点

## Task 1 Evidence

- RED：5 个 ReservationClient 测试均因类型不存在而失败。
- GREEN：`dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo`，5/5 通过。
- Helper build：`dotnet build research/SteamLobbyProbe/SteamLobbyProbe.csproj -c Release --nologo`，0 warning、0 error。
- Regression：`Test-ReservationProtocol.ps1`、`Test-LobbyJoinProtocol.ps1`、
  `Test-RealLobbyJoinProtocol.ps1` 均通过。

## Task 2 Evidence

- Baseline：`dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo`，5/5 通过。
- RED：加入 `L4d2HostSessionTests` 并链接尚不存在的 session 源文件后，测试项目按预期以
  `CS2001 L4d2HostSession.cs not found` 失败。
- GREEN：新增 `L4d2HostSession` 后，同一命令 11/11 通过。
- 覆盖：有效的真实 RequestJoinData fixture 产生带 endpoint 的 Reply；错误 chat room、发送者
  与 request ID 不同、`Creating` / `ReservationPending` / `Failed` 均没有 Reply。
- 诊断：第一次 GREEN 编译失败为 `CS0051`，根因是公开 MSTest 方法泄漏 internal enum 参数；
  测试签名改为 `int`、方法内部转换后通过。production session 的状态边界没有改变。

## Task 3 Evidence

- RED：加入 `L4d2LobbyChatMessageTests` 并链接尚不存在的 helper 后，测试项目按预期以
  `CS2001 L4d2LobbyChatMessage.cs not found` 失败。
- 实现前读取 SteamKit2 `3.4.0` 源码：`MsgClientChatMsg` 是 20 字节的 legacy body，payload
  为独立 `MemoryStream`；`ClientMsgHandler.Client.Send()` 是公开的发送边界。
- 诊断：首次 helper 编译失败为 `CS0246 MsgClientChatMsg`。该类型定义在
  `SteamKit2.Internal`，加入该命名空间后同一命令 12/12 通过。
- 覆盖：outbound raw payload 逐字节、长度都与输入相同，末字节没有被追加 `0x00`；chat room、
  chatter 和 `EChatEntryType.ChatMsg` 正确。handler 仅分支 `EMsg.ClientChatMsg`，其他消息不读取。

## Task 4 Evidence

- RED：真实 metadata 测试先调用不存在的 `CreateSessionMetadata()`，按预期以 `CS0117` 失败。
- GREEN：`LobbyMetadata` 改为共享的 `RealSessionSettings.CreateLobbyMetadata()` 后，插件测试
  12/12 通过；验证 28 个 metadata 字段中的 `Game:Mode=versus`、`Game:state=game`，并拒绝
  `Server:*` metadata。
- ASF build：`dotnet build research/L4d2AsfPlugin/L4d2AsfPlugin.csproj -c Release --nologo
  -p:ASFSourcePath='C:\\Users\\Administrator.DESKTOP-465SP1L\\AppData\\Local\\Temp\\asf-src-6.3.8.4'`
  成功，0 warning、0 error。
- 退休：旧的 private/probe metadata、固定 20 秒 delay 和自动 `LeaveLobby` 已删除；无配置或无
  enabled bot 时不产生 Steam mutation。新路径的 timeout 仅进入 `ReservationStatusRequired`，不会
  报告服务器接受。

## Task 5 Preparation

- 本地完整回归：`L4d2Protocol` 5/5、`L4d2AsfPlugin` 12/12 通过；
  `Test-ReservationProtocol.ps1`、`Test-LobbyJoinProtocol.ps1`、
  `Test-RealLobbyJoinProtocol.ps1`、`Test-RealSessionSettings.ps1` 全部通过。
- 远端只读检查：本机 SSH 配置将 `100.72.137.92` 解析为 `administrator@100.72.137.92:22`。
  `ssh -o BatchMode=yes ...` 返回 `Permission denied (publickey)`；未复制插件、未重启容器、
  未创建远端 lobby。
- 阻塞：需要该主机的可用 SSH 私钥路径，或由用户启用的其他认证方式，才能继续 Docker staging。

## TodoCheckpointDraft

- 当前 todo：Task 5，Docker staging 和客户端 transport 验收。
- 活动切片：只读检查已完成；等待远端 SSH 认证后复制 Release 文件并重启 ASF。
- 已完成：设计、计划、基线读取、共享协议库和 UDP client、配置/host session、raw chat transport、
  配置驱动 lifecycle、完整本地回归和 Release build。
- 证据：Task 1 protocol 回归；Task 2 的 11/11、Task 3/4 的 12/12；Task 4 Release build。
- 阻塞：`administrator@100.72.137.92` 拒绝本机所有默认公钥；没有执行远端 mutation。
- 下一步：获得可用 SSH 认证后，部署 `ReservationEnabled=false` 到 24561 并收集真实 JoinData 证据。

## DriftCheckDraft

- 范围：仍是 ASF 移植，不引入 MatchFramework 或服务器配置修改。
- 兼容：helper CLI 的参数、输出和 PowerShell fixed vectors 已回归通过。
- 退休：helper 中的五个协议源已移至 L4d2Protocol；同步 CLI transport 保留为诊断入口。
- 决定：continue。

## 2026-08-16 Steam lobby 可加入性隔离实验

### 复现

- ASF 创建 lobby `109775242232209903`，并向 `202.105.108.88:27085` 发出 reservation；服务器保留的 cookie 与 lobby CSteamID 一致。
- Edge 真实 Steam 会话（`76561199382197988`）直接调用 `ISteamMatchmaking::JoinLobby`，得到 `LobbyEnter_t` 的 `response=2`；L4D2 控制台对应显示 `CSysSessionClient: lobby response 2`。
- 这发生在游戏 `SysSession::RequestJoinData` 之前。ASF 没有 `join_data=RequestReceived` 日志，因此不能归因为 ReplyJoinData 编码或服务器 reservation。

### 只读反证

Edge 用只读 `RequestLobbyData` 读取同一 lobby 成功：owner 为 ASF bot，28 个 L4D2 metadata 均可见，且 `Game:state=game`。因此 response 2 不是 URI 格式、下载地区、metadata 不可见或请求未送达造成的。

### 唯一会话对照

- ASF 日志在失败轮次反复出现账号正被其他 Steam 会话使用的提示；该 bot 与先前本机 Steam API 对照创建所用账号相同。
- 确认本机 Steam 已退出后，重启独立 `archisteamfarm` 容器，使用相同插件配置建立新 lobby `109775242234405718`。
- Edge 对新 lobby 的同一 `JoinLobby` probe 返回 `response=1`，成员快照为 owner 与 Edge 两个 SteamID；5 秒后 probe 按设计调用 `LeaveLobby`。

### 结论与当前边界

- 已确认根因：owner 账号并发 Steam 会话会使 ASF 创建的 lobby 仍可被读取，却被 Steam 后端以 `LobbyEnter response=2` 拒绝加入。创建/运行时必须保证 bot owner 账号没有其他 Steam Desktop 或游戏会话。
- 此结论与 UDP reservation 独立：两轮都能向服务器发送 cookie reservation；修复的是 Steam lobby 成员关系，不是服务器 reservation。
- 仍未完成真实 L4D2 客户端经 URI 进入 `27085`：URI 会启动 Edge 的 L4D2，但之后只读成员表仍只有 owner，ASF 也未收到 `RequestJoinData`。下一假设是 bot 尚未以 AppID 550 的游戏运行状态发布 session；需单独验证，不能把 `response=1` 误报为已入服务器。

## 2026-08-16 AppID 550 状态与 URI 复测

- 重启独立 ASF 容器后，插件创建新 lobby `109775242237560687`（cookie
  `0x018600004EB3C76F`），目标仍为 `202.105.108.88:27085`。插件的 reservation 调用使用
  `RealSessionSettings.EncodeReservationSettings()`，即 551 字节真实 settings；该调用本次仍记录为
  `Timeout`，不是空 settings 或显式拒绝。
- 通过 ASF IPC 临时执行 `PLAY sirp 550`，返回“Playing selected gameIDs: 550”。这只改变 bot 的
  Steam games-played 状态，没有改写 lobby metadata、插件配置或服务器配置。
- Edge 的交互式 Session 1 从已存在的启动任务重新启动 L4D2，任务返回 `0`，新进程 PID 为
  `13848`。随后分别测试了命令行 `+connect_lobby 109775242237560687` 与对运行中客户端打开
  `steam://joinlobby/550/109775242237560687/76561199012457364`。
- 两条入口均完成任务层面的调用，但 Edge 的只读 `RequestLobbyData` 快照仍只显示 owner，未出现
  Edge SteamID；ASF 日志也没有 `join_data=RequestReceived`。因此真实 L4D2 没有在 Steam lobby
  层加入，尚未到 ReplyJoinData 或 Source UDP 连接阶段。
- 这轮 Edge 启动时间为 02:46:03，而 reservation 请求在 02:43:47 完成；已超过此前观测到的约
  121 秒 reservation 存活期。因此本轮不能据此评价 27085 的入服结果，但能独立否定“只缺少 ASF
  `PLAY 550` 状态”以及“只需在已运行客户端打开 URI”这两个假设。

### 当前根因边界

- 已知：消除 owner 的并发 Steam 会话后，普通 `ISteamMatchmaking::JoinLobby` 可以返回 `response=1`。
- 已知：外部 ASF lobby 的完整 metadata、完整 reservation settings、bot 的 AppID 550 状态均不能让
  Edge 的真实 L4D2 自动接管该 lobby。
- 未知：L4D2 还需要哪些由其自身 MatchFramework/Steam client session 发布的状态，才能把 URI
  转化为 `JoinLobby` 与 `SysSession::RequestJoinData`。下一轮应在连接前确认游戏侧的 lobby 回调或
  直接在 L4D2 控制台触发可观察的加入命令，同时在 121 秒 reservation 窗口内记录 RCON。

### Steam API 与 URI 入口对照

- 为排除当前 lobby 被 Steam 后端拒绝的可能，Edge 在同一交互式 Session 1 中通过独立的 x86
  `SteamLobbyProbe` 直接调用 `ISteamMatchmaking::JoinLobby`。回调为 `response=1`，成员快照在
  5 秒保持期内为 owner `76561199012457364` 与 Edge `76561199382197988` 两人；probe 随后按设计
  `LeaveLobby`。这再次证明当前 owner 没有并发会话拒绝问题。
- 随后停止 Edge 的 `left4dead2.exe`，只保留 Steam Desktop，再对同一 lobby 重跑相同 probe；结果仍为
  `response=1`、`member_count=2`、`is_member=True`，五秒后正常离开。故 ASF 作为 owner 与
  `SteamLobbyProbe` 作为 member 的通用 Steam 加入路径不依赖 L4D2 进程，也没有失败。
- 将 URI 交给 `steam.exe` 作为参数无效后，又在同一交互式 token 下使用 Windows 协议处理器
  `Start-Process steam://joinlobby/550/109775242237560687/76561199012457364`。任务本身返回 `0`，但
  后续只读快照仍只有 owner，ASF 仍没有 `join_data`。
- 因而可以确定：失败不是 URI 被错误地当作普通 `steam.exe` 参数，也不是 Steam lobby 后端拒绝。
  缺失的是 L4D2 实际执行/接管 `JoinLobby` 的客户端状态转换；外部 URI 调度在这个运行态没有触发它。

## 2026-08-16 创建前 AppID 550 时序实验（已否定）

- 假设：ASF 之前在创建 lobby 后才执行 `PLAY 550`；若先发布 `ClientGamesPlayedWithDataBlob` 的 AppID 550
  状态，再创建 lobby，L4D2 可能会接受该 owner session。
- 为只改变这个变量，临时构建在 `CreateLobby` 前调用 ASF 的公开 `bot.Actions.Play([550])`，成功后等待
  2 秒；其余 metadata、reservation、raw JoinData handler 均未改变。新 lobby 为
  `109775242240436368`，日志确认顺序为“AppID 550 published”后再创建 lobby。
- Edge 于 03:13:07 从停止状态启动真实 L4D2，并使用 `+connect_lobby` 加入。只读 lobby 快照仍只有 owner，
  ASF 没有 `join_data`；因此该假设不成立。
- 停止 Edge L4D2 后，`SteamLobbyProbe` 对同一新 lobby 仍返回 `response=1`，并在保持期内报告两个有效成员。
  故时序实验没有破坏通用 Steam lobby 加入能力。
- 该临时开关、测试和远端 DLL/config 已恢复，不作为插件功能保留。剩余差异是原生 `steam_api.dll`
owner session 与 ASF SteamKit CM owner session 的能力差异，而不只是 `ClientGamesPlayed` 的发送顺序。

## 2026-08-16 原生 Steam API 与 SteamKit2 创建路径对照

- 已重新确认用户指出的关键反证：`SteamLobbyProbe` 通过原生 `steam_api.dll` 创建的 AppID 550 lobby
  可以由真实 L4D2 客户端加入。因此根因不是“外部进程创建 lobby”，也不是 metadata、URI 格式或
  reservation 本身。
- 与插件使用的 `SteamKit2 3.4.0` 源码对照表明，ASF 调用的是标准
  `EMsg.ClientMMSCreateLobby`。它会明确发送 `app_id=550`、`lobby_type`、`max_members`、
  `lobby_flags=0`、metadata、CM `cell_id`、观测到的 `public_ip` 和 owner persona；随后以
  `ClientMMSSetLobbyData` 更新完整 metadata。并不存在另一个未调用的 SteamKit2 CreateLobby API。
- 同一消息 schema 还定义了可选的 `network_ping_location`（field 10），而 SteamKit2 3.4.0 的
  `CreateLobby()` 没有赋值。该字段不能通过公共 lobby metadata 读取，尚无证据证明 L4D2 以它为
  加入条件，不能作为修复依据。Docker bridge/NAT 也只能影响 `public_ip` 和 UDP；本问题发生在
  客户端发出 JoinData 之前，不能据此归因于 reservation 网络出口。
- 当前最小的严格对照仍是：以 ASF bot 同一账号启动原生 Steam Desktop/AppID 550 session，再由
  `SteamLobbyProbe` 创建 lobby，让 Edge 真实 L4D2 加入。ASF Docker 容器没有这样的本地 Steam
  Desktop session；SteamCMD 也不提供 `ISteamMatchmaking` / `SteamAPI_Init`，不能替代这一步。
  该实验会与 ASF 的同账号登录互斥，且需要用户提供或已登录的原生 Steam session，当前没有擅自
  停止 ASF 或读取 bot 凭据。

## 2026-08-16 SteamKit OGS app-session 单变量实验准备

- 读取 SteamKit 3.4.0 生成协议源码后，发现比 `ClientAppUsageEvent` 更接近 app-session 的 legacy
  消息：`MsgClientOGSBeginSession`（EMsg `5490`）。请求固定包含 `accountType`、完整
  `SteamID`、`appId` 和 Unix `timeStarted`；响应 `MsgClientOGSBeginSessionResponse` 返回
  `EResult`、采集开关和 `sessionId`。ASF 原生 `PlayGames()` 和现有插件均不发送该消息。
- 为单变量验证新增了临时 opt-in 配置 `PublishOgsBeginSession`，默认 `false`；开启时 handler
  在 `CreateLobby` 前发送 AppID 550 的 `ClientOGSBeginSession`，最多等待 3 秒响应，然后继续原有
  lobby、metadata、reservation 和 JoinData 路径。实验代码位于 `research/L4d2AsfPlugin`，未修改
  服务器配置、reservation 编码或凭据处理。
- TDD 证据：先加入 OGS 字段/响应测试，测试因两个新源文件不存在而 RED；实现后
  `dotnet test research\\L4d2AsfPlugin\\tests\\L4d2AsfPlugin.Tests.csproj --nologo` 通过
  `18/18`。随后 Release 构建命令使用 ASF 6.3.8.4 源码，结果为 `0` warning、`0` error。
- 远端验收尚未执行：`ssh -o BatchMode=yes administrator@100.72.137.92 hostname` 返回
  `Permission denied (publickey)`；ASF IPC `http://100.72.137.92:1242/Api` 可达但返回
  `401 Unauthorized`。因此没有复制 DLL、没有改远端 JSON、没有重启 `archisteamfarm`，也没有
  创建 Edge 临时任务。
- 当前结论仍是未知：只有在取得已授权的 SSH 私钥或 ASF IPC 认证后，才能判断 OGS BeginSession
  是否会让真实 L4D2 成为 lobby 成员并发送 `RequestJoinData`。本地 18/18 只证明消息编码和
  配置分支，不能证明真实客户端接受。

### 部署通道复核

- ASF IPC 认证现已可用：读取 custom plugins 返回 `L4d2LobbyProbePlugin`，`STATUS` 可成功查询
  默认 bot。密码仅通过 HTTP `Authentication` header 使用，未写入命令输出、文档或配置。
- 读取 ASF 6.3.8.4 的 IPC controllers 后确认，`/Api/Plugins` 仅列出和更新实现官方 update contract
  的插件；没有 custom DLL upload / arbitrary file write endpoint。当前插件也没有实现该 update contract。
- Docker Remote API 的已知管理端口 `2375`、`2376` 均未暴露。因此 IPC 不能代替 SSH/SFTP 将新的
  Release DLL 放入 `/home/sirp/archisteamfarm/plugins/L4d2AsfPlugin/`。
- 远端实验仍未发生 mutation。下一步需要一个受授权的文件部署通道；取得后可用现有 IPC 做状态读取、
  日志采集和 ASF restart 后的 OGS live test。

## 2026-08-16 SteamKit OGS app-session 实测（阴性，已退休）

- SSH 已按 `sirphomesv` 主机配置连接到 ASF Docker 主机。远端插件先以独立临时备份部署 OGS 实验版本，
  仅开启 `PublishOgsBeginSession`；没有改动 L4D2 服务器、reservation 编码、lobby metadata 或 Steam 凭据。
- 首次使用 `202.105.108.88:27085` 时，服务器显式拒绝 reservation，插件按既有策略离开 lobby；该轮不用于
  判断 OGS。随后仅把 endpoint 切至已知会保留 timeout lobby 的 `202.105.108.88:27084`。
- 有效轮中，日志顺序为 `ogs_begin sending app=550`、`ogs_begin result=OK`（返回非零 session ID）、
  `CreateLobby` 成功、再到 reservation `Timeout`。timeout 路径保持 lobby，符合该服务器必须以 status
  侧确认 reservation 的既有行为。
- Edge 通过原有 `warmsv` 的交互 token 启动真实 L4D2，并以 `+connect_lobby` 传入新 lobby ID。计划任务
  返回 `0`，且确认 `left4dead2.exe` 正在运行。
- 紧接着在相同 Edge Steam 身份运行只读 `Inspect-LobbyDiagnostic.ps1`：`RequestLobbyData=True`、
  完整 28 个 metadata 可读、owner 为 ASF bot、`MemberCount=1`，没有 Edge SteamID。ASF 同一时间窗口
  也没有任何 `join_data=RequestJoinData` 日志。只读 API 对非成员的 member index 返回 0，因此成员身份的
  结论以 member count、缺失 Edge SteamID 和 ASF JoinData 三项共同成立，而非将该 0 当作 SteamID。
- 结论：legacy `ClientOGSBeginSession` 对 AppID 550 的返回 `EResult.OK` 不是 L4D2 接受 SteamKit/ASF
  owner lobby 的充分条件。它没有让真实游戏执行 Steam lobby 加入状态转换，故不能作为 app-session
  缺失问题的修复。此结论只否定该单变量；原生 `steam_api.dll` 与 SteamKit CM owner session 的其余差异
  仍未知。
- 退休：删除 `L4d2OgsSession`、`L4d2OgsSessionHandler`、对应测试、JSON 开关和 lifecycle 分支；本地
  插件测试 15/15 通过，Release build 为 0 warning、0 error。远端已恢复原 DLL（哈希匹配）、原插件 JSON
  和健康容器；一次性远端备份以及两项 Edge 临时任务/输出均已删除。没有停止 Edge 的 L4D2 进程。
- SSH 可用后，另按请求删除 ASF 全局配置中的 `IPCPassword` 键；容器重启后配置仍不含该键，未认证的 tailnet
  IPC 请求返回 `403`。

## TodoCheckpointDraft

- 当前 todo：OGS 单变量实验已完成并退休；原始目标仍缺少能让真实 L4D2 接管 ASF owner lobby 的机制。
- 已完成：本地编码/handler 测试、远端 OGS 实测、Edge 真实客户端只读验收、远端回滚和临时资源清理。
- 证据：本节的 OGS `OK`、Edge `MemberCount=1`、缺失 `join_data`、本地 15/15 和 Release build，及远端
  hash/config/health 核验。
- 下一步：不应再次尝试相同 OGS 消息；如继续，应先建立原生 Steam owner session 与 SteamKit CM session 的
  可观测字段差异清单，并对每个字段做单变量实验。

## DriftCheckDraft

- 范围：实验保持在 ASF 插件与 Edge 客户端验证范围内；没有扩展到服务器配置、凭据或 MatchFramework 注入。
- 兼容：OGS 路径已整体删除，远端恢复部署前插件，因此正常 ASF reservation/JoinData 行为没有额外分支。
- 退休：代码、远端 DLL/config、备份和临时 Edge 计划任务均已收敛；当前没有遗留 fallback。
- 决定：continue（原始 lobby 可加入性目标尚未完成；OGS 假设已关闭）。
