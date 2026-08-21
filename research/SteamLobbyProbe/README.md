# Steam 大厅探测程序

该探测程序现在是 L4D1 专用入口：在不启动游戏客户端的情况下，用 AppID 500 创建和观察 Steam 大厅。

## 已验证结果

在 2026-08-14，Steam 正在运行且账户拥有 AppID 550 的前提下，探测程序在无 L4D2 进程的情况下完成了以下流程：

- `SteamAPI_Init=true`，AppID `550`，`BLoggedOn=true`
- `RequestLobbyList` 收到了有效的非空 `LobbyMatchList_t` 回调
- `CreateLobby(Private, 8)` 收到了 `LobbyCreated_t` 回调 513
- `m_eResult=1`（`k_EResultOK`），且大厅 ID 非零
- 写入了九个大厅数据键，并原样读回
- 通过 `LeaveLobby` 离开大厅

这证明可以从外部进程创建并填充一个普通的 Steam 大厅。但它并不证明 L4D2 客户端会把这个大厅当作完整的匹配会话接受。L4D2 可能仍然需要其自身的匹配状态、监听或专用服务器，以及预订/连接握手。

## 运行

构建一个自包含的 x86 探测程序：

```powershell
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o publish
```

从 `publish` 目录运行它，其中 `steam_appid.txt` 包含 `500`：

```powershell
.\SteamLobbyProbe.exe 'E:\SteamLibrary\steamapps\common\Left 4 Dead 2\bin\steam_api.dll'
```

默认预期 AppID 为 `500`。`STEAM_APP_ID` 和发布目录中的 `steam_appid.txt` 必须保持为 500：

```powershell
Set-Content -LiteralPath .\steam_appid.txt -Value '500' -NoNewline
$env:STEAM_APP_ID = '500'
.\SteamLobbyProbe.exe 'E:\SteamLibrary\steamapps\common\Left 4 Dead 2\bin\steam_api.dll'
```

程序会在初始化 Steam API 前同步设置进程级 `SteamAppId` 和 `SteamGameId`，并拒绝实际
AppID 与 `STEAM_APP_ID` 不一致的会话。不设置该变量时也使用 L4D1 AppID 500。

### Left 4 Dead 的 Steam API 兼容边界

2026-08-21 在 Windows x86 实测中，Left 4 Dead 自带的 `bin\steam_api.dll` 只导出旧式
`SteamMatchmaking`、`SteamUtils`、`SteamUser` 对象入口和 `SteamAPI_RunCallbacks`；它不导出
ManualDispatch 系列或 `SteamAPI_ISteam*` 扁平函数，不能被当前 Probe 安全加载。Left 4 Dead 2
自带的新版 x86 `steam_api.dll` 在 `STEAM_APP_ID=500`、`steam_appid.txt=500` 时成功初始化为
AppID 500，完成登录检查、Lobby 列表回调、私有 Lobby 创建、metadata 写入/读回和正常离开。

因此 Windows L4D1 研究使用兼容的新版 x86 Steam API 库，不使用 L4D1 自带的旧 DLL。这个
边界与 Linux Agent 使用 Steam Runtime 64 位 API 库、而非游戏自带 32 位库的部署原则一致。

### AppID 500 真实 Lobby 验证

2026-08-21 使用 `list-lobbies` 读取到一个真实 L4D1 公共 Lobby，取得 20 个 metadata 字段；
其中包括 `Game:campaign=Farm`、`Game:mode=coop`、`Members:numSlots=4`、
`Game:MissionInfo:DisplayTitle=#L4D360UI_Campaign_Farm` 和 L4D1 商店 URL。Probe 已将这组
实测字段固化为本分支唯一的 AppID 500 profile。

随后 Probe 创建一个最多保持 180 秒的 AppID 500 public Lobby，使用上述 20 个字段。一个真实
外部 L4D1 客户端发现并加入该 Lobby；Probe 依次收到：

- `LobbyChatUpdate` entered（状态 `0x00000001`）；
- `LobbyChatMsg` / `SysSession::RequestJoinData`，请求内含客户端机器与成员设置；
- 客户端在未收到 ReplyJoinData 后离开，`LobbyChatUpdate` 状态为 `0x00000002`；
- 180 秒到期后 Probe 正常 `LeaveLobby`。

该结果证明 L4D1 客户端能够发现并进入 Probe 创建的 AppID 500 Lobby，并会启动 Source
`SysSession::RequestJoinData` 协议；它尚不证明 ReplyJoinData、服务器连接或 reservation 已适配
L4D1。验证记录不保存外部客户端的 SteamID、昵称或原始身份 payload。

默认使用 Steam ManualDispatch 并消费实际回调事件。传入 `private`、`friends`、`public` 或 `invisible` 作为第二个参数以选择大厅类型。仅在进行 ABI/控制诊断时传入 `direct` 作为第三个参数；手动回调路径是该 DLL 的已验证路径。

只读列出当前 AppID 可搜索到的 Lobby ID、owner、成员数和完整 metadata：

```powershell
.\SteamLobbyProbe.exe <steam_api.dll> list-lobbies
```

## Steam API 参数

`ISteamMatchmaking::CreateLobby` 接受：

- `ELobbyType`：`Private`、`FriendsOnly`、`Public` 或 `Invisible`
- `maxMembers`：Steamworks 最多允许 250；L4D2 的正常大厅大小为 8

创建后，大厅所有者可以调用 `SetLobbyData(key, value)`，并传入字符串键和值。Steam 不会验证这些键在 L4D2 中的含义。可以使用 `GetLobbyData`、`GetLobbyDataCount` 和 `GetLobbyDataByIndex` 进行检查。

## 观察到的 L4D2 字段

以下名称出现在 L4D2 的 `matchmaking.dll` 中，可能是游戏元数据字段。它们出现在二进制中并不代表外部进程写入的每个字段都会被当作权威字段接受。

| 分组 | 键 |
| --- | --- |
| 游戏 | `game:mode`、`game:map`、`game:campaign`、`game:chapter`、`game:difficulty`、`game:state`、`game:vanilla`、`game:maxrounds`、`game:sk_versus`、`game:dlcrequired` |
| 模式信息 | `game:ModeInfo`、`game:ModeInfo:DisplayTitle`、`game:ModeInfo:workshopid`、`game:ModeInfo:addon` |
| 任务信息 | `game:MissionInfo`、`game:MissionInfo:Version`、`game:MissionInfo:BuiltIn`、`game:MissionInfo:DisplayTitle`、`game:MissionInfo:Author`、`game:MissionInfo:Website`、`game:MissionInfo:workshopid`、`game:MissionInfo:addon` |
| 系统 | `system:network`、`system:access`、`system:netflag`、`system:lock`、`system:dependentlobby` |
| 选项 | `options:server`、`options:searchteamkey`、`options:serverlist`、`options:action` |
| 成员 | `members:numSlots`、`members:numPlayers`、`members:numMachines` |
| 服务器 | `server:name`、`server:server`、`server:adronline`、`server:adrlocal`、`server:connectstring`、`server:reservationid`、`server:ping`、`server:team`、`server:xuid` |

在 L4D2 日志或二进制支持的元数据中出现的值包括 `LIVE`、`public`、`listen`、`dedicated`、`coop`、类似 `c1m1_hotel` 的地图名称，以及数字形式的成员/对抗值。确切的有效值集合由 L4D2 控制，并不由通用 Steam 大厅 API 定义。

服务器和预订字段不是普通的自由格式配置：它们可能依赖真实服务器、Steam 会话信息或后端预订。写入看似合理的字符串并不等于完成该握手。

## 真实客户端对照实验

2026-08-15 使用真实 L4D2 客户端和 `106.54.197.3:24561` 完成了对照实验。该服务器设置为 `sv_allow_lobby_connect_only=0`，连接时报告：

```text
Server using '<none>' lobbies, requiring pw no, lobby id 0
```

探测程序创建 lobby `109775242109682143`，并在 lobby metadata 与 `SysSession::ReplyJoinData` 中写入：

```text
server:adronline=106.54.197.3:24561
server:adrlocal=106.54.197.3:24561
server:connectstring=106.54.197.3:24561
server:reservationid=109775242109682143
```

远端账号 `76561199382197988` 加入 lobby 后，探测程序收到 `SysSession::RequestJoinData`，并成功发送 632 字节的 `SysSession::ReplyJoinData`。客户端随后通过 Reply 中的地址再次连接服务器，日志出现：

```text
Connecting to public(106.54.197.3:24561)
Server using '<none>' lobbies, requiring pw no, lobby id 0
CSteam3Client::InitiateConnection: 106.54.197.3:24561
Connected to 106.54.197.3:24561
Map: c2m1_highway
#Cstrike_TitlesTXT_Game_connected
```

这证明当前的 lobby 加入消息、Reply 编码和 `connectstring` 地址传递可以驱动真实客户端进入游戏。服务器仍显示 `lobby id 0` 是预期行为：它不处理 lobby reservation，因此不会采用 Reply 中的 `reservationid`。

这个对照实验本身不能证明要求 lobby reservation 的服务器已经可用。当时
`202.105.108.88:27084` 仍会返回 `Server error - failed to handle reservation request.`，因此问题被限定在
服务器预订握手，而不是 `connectstring` 生成。服务器接受指定 cookie 的最小 reservation 请求已在后文
“真实 reservation 握手复现”中完成复现。

短时间反复创建测试 lobby 时，Steam 可能返回 `LobbyCreated result=25`。官方含义是当前游戏客户端创建了过多 lobby 并受到限流。测试 helper 应正常退出并调用 `LeaveLobby`；若 helper 被强制结束后 Steam 仍把 AppID 550 进程视为活动会话，可在确认本机没有游戏运行后重启 Steam，再等待其重新登录。

## 其他 API 路径

Steam 还在 `https://partner.steam-api.com/ILobbyMatchmakingService/CreateLobby/v1/` 上公开了 `ILobbyMatchmakingService/CreateLobby`。该端点需要发布者 Web API 密钥，并不是面向普通用户的、替代客户端 `ISteamMatchmaking` API 的方式。

## 大厅成员关系诊断命令

探针还提供三个仅用于实验的命令：

```powershell
# 创建一个普通大厅并保持回调，不绑定游戏服务器
.\SteamLobbyProbe.exe <steam_api.dll> create-lobby-hold private 30

# 加入一个已存在的大厅并保持回调
.\SteamLobbyProbe.exe <steam_api.dll> join-lobby-hold <lobbyId> 30

# 离开指定大厅，并记录调用前后的成员快照
.\SteamLobbyProbe.exe <steam_api.dll> leave-lobby <lobbyId> 3
```

`leave-lobby` 输出中的 `member_count` 是 Steam API 的原始缓存值；离开后它可能暂时不清零。`valid_member_count` 会忽略 `GetLobbyMemberByIndex` 返回的 SteamID `0`，`is_member=False` 才表示当前账号已不在有效成员列表中。

当 L4D2 已在登录用户桌面会话运行时，这些命令也必须在同一个交互式 Windows 会话执行。通过 SSH 的 Session 0 直接启动可能得到 `SteamAPI_Init=false`；测试环境使用 `LogonType=Interactive` 的计划任务运行探针。

## 离开大厅但保留服务器连接

2026-08-15 在 `106.54.197.3:24561` 上完成了有效重试：

- 原大厅：`109775242118474838`
- 本地 owner：`76561199012457364`
- 远端真实客户端：`76561199382197988`，PID `10016`
- `LeaveLobby` 前：大厅有两个有效成员，A2S 连续三次显示玩家 `Z。`
- `LeaveLobby` 后：远端探针显示 `is_member=False`，本地 helper 收到 `LobbyChatUpdate state=0x00000002`
- 随后 12 次连续监控中服务器始终可响应 A2S_INFO，A2S_PLAYERS 始终保留 `Z。`
- 延迟复核时玩家连接时长继续增长到 122.5 秒，L4D2 进程仍响应
- 额外使用 `Start-L4D2CondebugDiagnostic.py` 启动日志化客户端复核：`console.log` 新鲜输出 `Connected to 106.54.197.3:24561` 和 `#Cstrike_TitlesTXT_Game_connected`；执行 `LeaveLobby` 后没有新增 disconnect 或返回大厅行，A2S 连续八次仍为 1

结论：在该对照服务器上，Steam lobby membership 与已经建立的游戏网络连接可以解耦。远端账号可以离开原大厅而不退出 24561；本地 helper 也无需转让 owner。

第一次相同实验期间 24561 恰好崩溃，A2S 变空。该轮结果已作废，不能作为 `LeaveLobby` 导致掉线的证据；有效重试同时监控了 A2S_INFO，确认服务器全程在线。

第二大厅实验最初受到 Steam `LobbyCreated result=25` 限流。重启本机 Steam 后创建成功，并完成了真实验证：

- 本地软件创建第二大厅 `109775242119301208`，owner 仍为 `76561199012457364`
- 远端账号在保持 24561 连接时执行 `join-lobby-hold 109775242119301208 30`
- `LobbyEnter_t` 返回 response `1`，成员快照显示两个有效成员
- 第二大厅 helper 收到远端加入 `state=0x1`，30 秒后收到离开 `state=0x2`
- 保持期内连续 10 次 A2S 均为服务器在线、玩家数 1
- 远端离开第二大厅后 A2S 仍为 1，玩家连接时长增长到 593.3 秒

因此，“本地创建新大厅并让已在游戏服务器中的远端账号加入”也不会中断现有 Source 网络连接。远端账号不必成为新大厅 owner；若必须由远端账号直接调用 `CreateLobby`，仍可能受 Steam 创建频率限制，需要等待冷却或重启该账号的 Steam 客户端。

`Start-L4D2CondebugDiagnostic.py` 是远端取证脚本：它复用 `C:\Users\Administrator\Desktop\warmsv` 中的 Steam 路径和进程稳定性检查，只在启动参数中增加 `-console -condebug`。它不是大厅或连接实现的一部分。

## 真实 reservation lobby 的只读检查

真实 L4D2 客户端连接要求 `sv_allow_lobby_connect_only=1` 的服务器后，服务器 status 显示：

```text
players : 3 humans, 0 bots (12 max) (not hibernating) (reserved 186000047bb2ba4)
```

其中 cookie 的两种表示为：

```text
hex     = 0x0186000047BB2BA4
decimal = 109775242120604580
```

该数值本身就是对应 Steam lobby 的 CSteamID。使用只读脚本检查：

```powershell
.\Inspect-LobbyDiagnostic.ps1 -LobbyId 109775242120604580
```

脚本会自动转入 32 位 PowerShell，并且只导入 `RequestLobbyData`、metadata Get、owner/member Get 和 `GetLobbyGameServer`。它不包含 Set、Join、Leave 或 owner 修改 API。

最初两次连续读取结果一致：

- owner：`76561199012457364`，即本机真实 L4D2 使用的 Steam 账号。
- 当时的 lobby 成员：只有 owner 一个账号。
- `Game:state=game`，说明大厅已进入游戏态。
- 共有 28 个 metadata，没有任何 `Server:*` 键。
- `GetLobbyGameServer=False`，IP、端口和 server SteamID 都为 0。

完整 metadata：

```text
Game:campaign=L4D2C2
Game:chapter=1
Game:difficulty=normal
Game:dlcrequired=0
Game:maxrounds=3
Game:MissionInfo:addon=0
Game:MissionInfo:Author=Valve
Game:MissionInfo:builtin=1
Game:MissionInfo:DisplayTitle=#L4D360UI_CampaignName_C2
Game:MissionInfo:InfectedOnly=0
Game:MissionInfo:MissionFile=missions/campaign2.txt
Game:MissionInfo:SurvivorSet=2
Game:MissionInfo:Version=1
Game:MissionInfo:Website=http://store.steampowered.com
Game:MissionInfo:workshopid=0
Game:Mode=versus
Game:ModeInfo:addon=0
Game:ModeInfo:workshopid=0
Game:sk_versus=35
Game:state=game
Game:vanilla=1
Members:numMachines=1
Members:numPlayers=1
Members:numSlots=8
Options:Server=official
System:access=public
System:lock=
System:network=LIVE
```

后续收尾校验时，另一个账号 `76561198680619771` 已加入大厅，动态字段更新为：

```text
MemberCount=2
Member[0]=76561199012457364 owner=True
Member[1]=76561198680619771 owner=False
Game:sk_versus=37
Members:numMachines=2
Members:numPlayers=2
```

这次变化没有增加 `Server:*` 字段，`GetLobbyGameServer` 仍为 false。服务器 status 的 3 名 human 与 lobby 的 2 名成员不是同时采样，中间存在玩家临时进出，因此不能用这两个数字判断服务器玩家集合与 lobby member 集合的关系。

结论：服务器采用 reservation cookie 的事实只能从服务器状态直接确认；在该时刻，普通 Steam lobby metadata 和 `ISteamMatchmaking::GetLobbyGameServer` 都没有暴露服务器地址或 reservation 字段。真实 L4D2 使用的是游戏 MatchFramework/网络协议发起的 reservation 请求，服务器以 lobby CSteamID 作为 cookie 保存 reservation。此前 probe 手工写入的 `server:reservationid` 和 `server:connectstring` 只是模拟字段，不能替代这次真实握手。

这个结果还说明：要生成正确的 `reservationid`，数值层面应使用真实 lobby CSteamID；剩余核心不是再造一个不同格式的 ID，而是复现真实客户端让服务器接受该 ID 的 reservation 请求，并把实际服务器地址通过 L4D2 的会话加入协议交给其他客户端。

## 真实 reservation 握手复现

2026-08-15 已复现 L4D2 的 connectionless UDP reservation 请求，并让
`202.105.108.88:27084` 接受 cookie：

```text
hex     = 0x0186000047CF0FD8
decimal = 109775242121908184
status  = reserved 186000047cf0fd8
```

新增的两个命令不加载 `steam_api.dll`：

```powershell
# 离线验证 challenge、ICE 和 reservation packet 固定向量
.\SteamLobbyProbe.exe reservation-vector 0x12345678 0x0186000047CF0FD8 2243

# 对目标服务器执行 challenge -> reservation
.\SteamLobbyProbe.exe reserve-server 202.105.108.88:27084 0x0186000047CF0FD8 5000 2243
```

协议关键值：

```text
A2S_GETCHALLENGE     = 'q' = 0x71
S2C_CHALLENGE        = 'A' = 0x41
A2S_RESERVE          = 'n' = 0x6E
S2A_RESERVE_RESPONSE = 'p' = 0x70
GetHostVersion()     = 2243
```

challenge 请求必须包含结尾 NUL：

```text
FFFFFFFF71726573657276653030303030303000
```

服务器返回 challenge `0x03090BC4` 后，本次最小请求使用零长度 settings。加密前 payload 为：

```text
EFBEEDFE D80FCF4700008601 00000000
magic    cookie           settingsLength
```

ICE key 由 challenge 与两个固定常量异或后按小端组成，每 8 字节独立使用 ICE level 1 加密。本次实际发送：

```text
FFFFFFFF6EC308000010000000753BBB7473666073136FE3303EAAD11D
```

发送前服务器 RCON `status` 为：

```text
players : 0 humans, 0 bots (12 max) (not hibernating) (unreserved)
```

发送后立即变为：

```text
players : 0 humans, 0 bots (12 max) (not hibernating) (reserved 186000047cf0fd8)
```

这直接证明 reservation packet、host version、ICE key、cookie 字节序和最小 payload 均正确。该服务器在本次
`settingsLength=0` 请求后没有在 5 秒内发送 `S2A_RESERVE_RESPONSE`，所以命令输出
`ReservationResponse timeout=True ... verification=server_status_required` 并返回非零状态。exact L4D2
`engine_srv.so` 中该回包只有 `hostVersion + success bit`，且成功路径先保存 reservation 状态；不能把“未即时收到回包”
等同于“服务器拒绝”。本次接受事实由服务器 `status` 直接确认。

当前结论：

- 正确 `reservationid` 就是 lobby CSteamID，本例为 `0x0186000047CF0FD8`。
- 正确 `connectstring` 仍是实际目标地址，例如 `202.105.108.88:27084`。
- 普通 Steam lobby metadata 中的同名字段不能替代 UDP reservation 握手。
- 带真实 lobby game settings 的 binary KeyValues 已编码完成，实际 settings 长度为 `551` 字节；reservation
  完成与否必须以目标服务器 RCON 的 `reserved <cookie>` 为准，不能只看客户端工具是否收到 UDP response。
- 真实客户端入服和 owner 离开 lobby 后的持续性，见下节实验记录。

### 27085：无人入服时 reservation lease 最多约两分钟

2026-08-16 对 `202.105.108.88:27085` 做了 5 秒间隔的 RCON `status` 采样。新 lobby 的 cookie 为
`18600004e4d8b16`；probe 本身保持 180 秒，但服务器 reservation 在没有玩家入服时提前失效：

```text
首次观察到 reserved：2026-08-16 01:36:32.410
最后一次观察到 reserved：2026-08-16 01:38:32.381
首次观察到 unreserved：2026-08-16 01:38:37.365
```

因此，从首次 RCON 确认 `reserved` 起，服务器至少保留了 `119.971` 秒，并在随后 `4.984` 秒采样窗口内
变为 `unreserved`，可归纳为**无人入服时 reservation lease 最多约两分钟**。这不是 Steam lobby 的生命周期：
本轮 helper 仍按 180 秒保持 lobby，服务器 lease 已先行过期；helper 最终 `LeaveLobby` 后，独立 RCON
`status` 仍为 `unreserved`。

## 27082 与 27083 实际入服测试

### 27082：本机出口被拒绝

测试 lobby `109775242168041796`，cookie 为 `0x018600004A8F0144`。本机 helper 完成 challenge 并发送了完整
settings，但 RCON 始终为 `unreserved`。edge 虽然收到了 helper 的 `ReplyJoinData`，客户端随后输出：

```text
Server error - failed to handle reservation request.
```

这说明 `ReplyJoinData sent=True` 只代表 Steam lobby 消息发送成功，不代表游戏服务器接受 reservation。

### 27083：从 edge 发送 reservation 后成功

本机对 `27083` 发送 challenge 没有 UDP 回包，但 edge 发送相同 challenge 能正常收到 `S2C_CHALLENGE`。因此把
reservation packet 的发送位置移到 edge：本机使用 `real-settings-vector` 生成当前 cookie 的完整 packet，edge
原样发送。RCON 最终确认：

```text
lobby       = 109775242170052468
cookie      = 0x018600004AADAF74
connect     = 202.105.108.88:27083
RCON        = reserved 18600004aadaf74
edge player = 58.55.184.63:27005
```

edge 通过大厅 URI 进入后，RCON 为 `1 humans`，玩家状态为 `active`。随后 owner 执行 `LeaveLobby`，最后一次
RCON 仍保持 1 个 active human 和相同 reservation；只读 lobby 查询显示 owner 自动转移到 edge，且
`server:connectstring`、`server:reservationid`、`GetLobbyGameServer` 均保留。

本轮 `27083` 为了让本机 helper 在本机无法收到 challenge 时仍能提供大厅回调，使用了现有 `server` 模式的
legacy ReplyJoinData；它的 `Server` 字段与 reservation cookie 正确。`27084` 的成功实验则验证了
`server-reserved` real ReplyJoinData。两轮共同证明：客户端入服的关键前提是服务器实际保留了 lobby cookie，且
reservation packet 的网络出口必须能收到目标服务器的 challenge。

### 手动 `connect` 对照

为验证 IP 直连是否绕过 Steam lobby，新建 lobby `109775242171763520` 并预留：

```text
cookie = 0x018600004AC7CB40
RCON   = reserved 18600004ac7cb40
```

没有向 edge 分发 lobby URI；仅由用户在 edge L4D2 控制台执行：

```text
connect 202.105.108.88:27083
```

RCON 显示 edge `58.55.184.63:27005` 为 active human，且仍保留相同 cookie。与此同时 helper 收到 edge 的
`RequestJoinData` 并回传 `ReplyJoinData`；只读 Steam 查询得到 2 个成员（owner 与 edge）。因此，在已 reservation
的 `27083` 上，手动 IP 直连没有绕过 lobby，而是同时触发了对应 lobby 的加入协议。该结论只覆盖服务器已经接受
该 cookie 的情形；未 reservation 的 IP 直连仍须单独测试。

## Steam 下载地区与大厅匹配

Steam 客户端的“下载地区”和 Steam lobby 的“距离筛选”是两个不同层次的机制，不能把它们当成同一个地区字段。

- 当前 Steam 客户端把下载地区保存为内部设置 `download_region`（整型设置号 `8009`），下拉选项来自客户端取得的
  `vecValidDownloadRegions`。这个值用于 SteamPipe 内容下载源的选择。
- 官方 `ISteamMatchmaking::AddRequestLobbyListDistanceFilter` 文档规定，lobby 的物理距离按**用户公网 IP**和 Steam
  后端的 IP 地理位置映射计算；并提供 `Close`、`Default`、`Far`、`Worldwide` 四种范围。文档没有把下载地区作为
  该接口的输入。
- Steam lobby 协议的 `CMsgClientMMSGetLobbyList` 请求会显式上传 `cell_id` 和 `public_ip`。本机公网 IP 地理定位为
  中国青岛，但 `CellIDServerOverride=64` 时，连接日志显示实际请求 `cellid=64` 并优先连接 `lax1`；同一时段的内容源
  也是 `*-lax1`。因此，下载地区所选择/覆盖的 Cell 是 lobby 搜索的明确输入，能够改变服务器看到的搜索地域上下文。
- Steamworks 文档只说明距离“基于用户 IP 和 Steam 后端 IP location map”，没有公开 `cell_id` 与 `public_ip` 的权重、
  优先级或冲突处理。这不能被误读为 `cell_id` 不参与搜索；协议实际同时提交了两者。
- 对当前 L4D2 `matchmaking.dll` 的静态检查发现 `SessionSearch`、`Filter=/Game:*` 与 `Near/*` 搜索字段，但没有
  `download_region`、`DownloadRegion` 或等价配置键。这说明 L4D2 不必自行读取下载地区：它通过 Steam API 发起搜索时，
  Steam 客户端已把当前 CellID 和公网 IP 带入底层 lobby 请求。

可以手动设置下载地区：打开 Steam 客户端的“Steam -> 设置 -> 下载”，在“下载地区”中选择目标地区，然后按客户端
提示重启 Steam。不要手动改 `D:\Steam\config\config.vdf` 里的 `CurrentCellID` 或 `CellIDServerOverride`：它们是 CM
连接路由状态/调试覆盖项。`CellIDServerOverride` 确实会影响 CM、内容源以及 lobby 请求中的 `cell_id`，但它绕过了 Steam
设置界面的有效地区管理，容易与 UI 所选下载地区、缓存和当前登录状态不一致；应只把它作为受控诊断开关，而不是常规设置方式。

若要量化下载地区对本环境的 L4D2 搜索结果的影响，应保持账号、公网出口、游戏搜索条件和测试 lobby 不变，只切换
下载地区并完整重启 Steam；随后记录实际 `cellid`、CM 数据中心、L4D2 返回的 lobby 集合和排序。这样才能区分 CellID、
公网 IP、lobby 生命周期和普通搜索波动各自的影响。

官方接口说明：<https://partner.steamgames.com/doc/api/ISteamMatchmaking#AddRequestLobbyListDistanceFilter>

协议实现参考（可看到 `GetLobbyList` 同时写入 `cell_id` 与 `public_ip`）：
<https://github.com/SteamRE/SteamKit/blob/master/SteamKit2/SteamKit2/Steam/Handlers/SteamMatchmaking/SteamMatchmaking.cs>

## 原 owner 离开后的 URI 重入

2026-08-15 在允许普通 IP 连接的对照服务器 `106.54.197.3:24561`
（`sv_allow_lobby_connect_only=0`）完成了完整重入测试：

```text
lobby = 109775242179079768
URI   = steam://joinlobby/550/109775242179079768/76561199012457364
server= 106.54.197.3:24561
```

测试顺序及结果：

1. 本机 probe 创建 `game` 状态的 public lobby，写入 `server:connectstring`、`server:reservationid`，并设置
   `GetLobbyGameServer=106.54.197.3:24561`。
2. edge 通过该 URI 加入。probe 收到其 `SysSession::RequestJoinData` 并成功回复；edge 的真实客户端日志确认
   `Connected to 106.54.197.3:24561`，A2S 显示 1 名人类玩家。
3. 本机 owner 调用 `LeaveLobby`。只读 Steam 查询随后显示 owner 已变为 edge，server metadata 和
   `GetLobbyGameServer=True` 均保留，A2S 仍为 1 名人类玩家。
4. 停止创建 probe 后，本机真实 L4D2 仅重新调用最初记录的 URI，没有额外执行 IP `connect`。A2S 随后变为 2 名人类
   玩家；只读 lobby 查询显示本机账号与 edge 均为成员，edge 仍是 owner，且 `Game:state=game`、
   `server:connectstring=106.54.197.3:24561`、`GetLobbyGameServer=True` 都保持不变。

结论：在这个不要求 reservation 的服务器上，URI 中的第三段旧 owner SteamID 不会在其离开 lobby 后使 URI 失效；旧 owner
可以通过原 URI 重新加入 lobby 并进入已连接的服务器。该结论不证明需要 reservation 的服务器也具有相同行为，后者仍取决于
服务器是否保留 reservation cookie。

随后再次分发同一原始 URI，用户确认本机真实客户端可正常进入。这与上述 A2S 和 Steam lobby 成员读取一致。确认完成后，
用户已退出本机和 edge 的 L4D2 客户端，因此不应把后续的服务器人数或 lobby 存活状态解释为本轮仍在运行。
