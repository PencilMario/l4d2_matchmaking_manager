# 验证证据

## 2026-08-14 基线

- `dotnet build -c Release`：exit 0，0 warning，0 error。
- 现有 `publish/SteamLobbyProbe.exe <steam_api.dll> private`：exit 0。
- Steam API：`SteamAPI_Init=true AppID=550`、`BLoggedOn=True`。
- 创建 lobby：`LobbyCreated result=1`，非零 lobby ID。
- 九个既有字段均 Set/Get 成功，随后 `LeaveLobby complete`。
- 目标服务器此前 A2S_INFO：在线、AppID 550、`c2m1_highway`、0/12、VAC secure。

## 待补证据

- L4D2 `+connect_lobby` 客户端日志。
- 连接期间的服务器 A2S 状态。

## Task 1 RED

- 命令：`powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-ServerLobby.ps1`
- 结果：exit 1。
- 预期失败：现有程序把第二个参数 `server` 传给 `ParseLobbyType`，抛出 `System.FormatException: The input string 'server' was not in a correct format.`
- 结论：测试确实覆盖新增 server 子命令，失败与 Steam 登录、构建或网络无关。

## Task 2 GREEN

- 目标测试：`powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-ServerLobby.ps1`，exit 0，输出 `Server lobby contract verified`。
- 创建 lobby：`109775242099181787`。
- metadata：`server:reservationid=109775242099181787`，等于 lobby ID；`server:connectstring=202.105.108.88:27084`。
- Steam binding：`GetLobbyGameServer ok=True ... ip=202.105.108.88:27084 steam_server_id=0`。
- 回调：收到 callback 509，即 `LobbyGameCreated_t`。
- join 参数：`steam://joinlobby/550/109775242099181787/76561199012457364` 和 `+connect_lobby 109775242099181787`。
- 清理：`Keepalive complete seconds=1` 后 `LeaveLobby complete`。
- 旧模式回归：`.\publish-test\SteamLobbyProbe.exe <steam_api.dll> private` exit 0，原有九个字段仍可读回并正常 LeaveLobby。

## 2026-08-15 对照服务器端到端实验

### 对照条件

- 服务器：`106.54.197.3:24561`。
- 配置差异：`sv_allow_lobby_connect_only=0`，服务器不要求或处理 lobby reservation。
- A2S_INFO：在线，地图 `c2m1_highway`，游戏 `Left 4 Dead 2`，版本 `2.2.4.3`。
- 远端真实客户端：`100.77.230.2`，Steam ID `76561199382197988`，L4D2 PID `8104`。
- 直接连接基线：客户端成功输出 `Server using '<none>' lobbies`、`Connected to 106.54.197.3:24561` 和 `#Cstrike_TitlesTXT_Game_connected`。

### Lobby 与 Reply 证据

- helper PID：`35896`。
- lobby：`109775242109682143`，owner Steam ID `76561199012457364`。
- helper 日志：`research/SteamLobbyProbe/control-106-24561-20260815-002957.stdout.log`。
- metadata 读回：`server:adronline`、`server:adrlocal`、`server:connectstring` 均为 `106.54.197.3:24561`。
- metadata 读回：`server:reservationid=109775242109682143`，等于 lobby ID。
- helper 收到 callback 506，确认远端账号进入 lobby。
- helper 收到 callback 507，读取 254 字节 `SysSession::RequestJoinData`。
- helper 发送 632 字节 `SysSession::ReplyJoinData`，输出 `sent=True`。

### 客户端结果

客户端在直接连接成功记录之后，又新增了一组由 lobby Reply 触发的连接记录：

```text
Connecting to public(106.54.197.3:24561)
Sending UDP connect to public IP 106.54.197.3:24561
Server using '<none>' lobbies, requiring pw no, lobby id 0
RememberIPAddressForLobby: lobby 0 from address 106.54.197.3:24561
CSteam3Client::InitiateConnection: 106.54.197.3:24561
Connected to 106.54.197.3:24561
Map: c2m1_highway
#Cstrike_TitlesTXT_Game_connected
```

### 结论与边界

- 已证明：`RequestJoinData -> ReplyJoinData -> connectstring -> UDP connect` 路径可以让真实 L4D2 客户端进入游戏。
- 已证明：此前 `0.0.0.0:27015` 问题由缺少 `Server/adronline` 和 `Server/adrlocal` 导致，当前地址字段已经正确。
- 预期现象：对照服务器显示 `lobby id 0`，因为它不消费 lobby reservation；这不代表 Reply 中的 `reservationid` 编码错误。
- 尚未证明：`202.105.108.88:27084` 所需的服务器 reservation 握手。该服务器仍可能返回 `failed to handle reservation request`，后续需要继续定位服务器预订请求，而不是修改已验证的 connectstring 路径。

### 运行环境问题

- 连续创建测试 lobby 后曾收到 `LobbyCreated result=25`。
- Steamworks 官方说明：`k_EResultLimitExceeded` 表示当前游戏客户端创建了过多 lobby，正在被限流。
- 本次还发现旧 helper PID `7364` 实际仍在运行，Steam 日志未出现对应的 `Game process removed`。正常重启本机 Steam 并重新登录后，下一次创建成功。

## Owner transfer 对照实验边界

- 对照 lobby：`109775242113812115`，服务器 `106.54.197.3:24561`。
- 将 owner 从本地账号转给远端账号后，`server:connectstring` 和 `server:reservationid` 读回保持不变。
- 原 helper 退出后，远端真实客户端离开 24561 并返回大厅；`game:state` 从 `game` 变为 `lobby`。
- 手动把 metadata 恢复为 `game:state=game` 没有再次触发服务器连接。
- 结论：metadata 和 owner 转让不能替代 L4D2 MatchFramework 的 StartGame 事件。该路径保留为负面证据，不再作为保持服务器连接的方案。

## 2026-08-15 不转让 owner 的 LeaveLobby 实验

### 无效轮次

- lobby：`109775242117811990`。
- 远端调用 `LeaveLobby` 后 helper 收到 `state=0x00000002`，但 24561 同时崩溃并停止响应。
- 因服务器状态是混杂变量，该轮 A2S 玩家消失不能归因于 `LeaveLobby`，结果作废。

### 有效重试基线

- 服务器恢复后 A2S_INFO：在线，`c2m1_highway`，0/12。
- 新 lobby：`109775242118474838`，owner `76561199012457364`，helper PID `35700`。
- 远端 L4D2：Steam ID `76561199382197988`，PID `10016`。
- helper 收到远端 `LobbyChatUpdate state=0x00000001` 和 RequestJoinData，并输出 `ReplyJoinData ... sent=True`。
- 操作前连续三次 A2S：`ONLINE;players=1;names=Z。`。

### LeaveLobby 动作

远端交互式任务运行：

```text
SteamLobbyProbe.exe <steam_api.dll> leave-lobby 109775242118474838 3
```

成员快照：

```text
LobbyMembership before ... member_count=2 valid_member_count=2 is_member=True members=76561199012457364,76561199382197988
LeaveLobby requested lobby_id=109775242118474838 settle_seconds=3
LobbyMembership after ... member_count=2 valid_member_count=0 is_member=False members=<none>
```

- 远端动作 exit `0`。
- 本地 helper 收到 `LobbyChatUpdate ... changed_user=76561199382197988 ... state=0x00000002`。
- 动作后连续 12 次查询均为 `ONLINE;players=1;names=Z。`。
- 延迟复核：`ONLINE;players=1`，玩家连接时长增长到 122.5 秒。
- 远端 `left4dead2.exe` PID `10016` 仍在运行且 `Responding=True`。

### 日志化复核

- 取证启动脚本：`research/SteamLobbyProbe/Start-L4D2CondebugDiagnostic.py`。
- 新 lobby：`109775242119792935`，远端 L4D2 PID `11096`。
- `console.log` 在本轮启动后由 17092 字节更新到 24226 字节，并新鲜输出：

```text
Connecting to public(106.54.197.3:24561)
Connected to 106.54.197.3:24561
Map: c2m1_highway
#Cstrike_TitlesTXT_Game_connected
```

- 执行 `leave-lobby 109775242119792935 3` 后，远端成员快照变为 `is_member=False`，helper 收到 `state=0x00000002`。
- 动作后 `console.log` 行数仍为 417，没有新增 disconnect、返回大厅或重新连接记录。
- 同期连续 8 次 A2S：服务器均在线且玩家数均为 1；PID `11096` 仍 `Responding=True`。

### 结论

- 已证明：远端真实客户端可以退出 helper-owned Steam lobby，同时保持到 `106.54.197.3:24561` 的游戏连接。
- 不需要转让 lobby owner；本地 helper 可以继续拥有原大厅。
- lobby 的 `LeaveLobby` 回调本身不会强制关闭已经建立的 Source 网络通道。

## 第二大厅实验状态

- 新增并本地验证：`create-lobby-hold`、`leave-lobby`、`join-lobby-hold`。
- `join-lobby-hold` 对受控大厅的契约测试 exit 0，并解析 `LobbyEnter_t` response `1`。
- 本地与远端后续 CreateLobby 曾收到 `LobbyCreated result=25`，属于 Steam 创建频率限制。
- 尝试重新加入已销毁的旧大厅时得到 `LobbyEnter response=2`，同时 24561 仍 `ONLINE;players=1`。

### 有效第二大厅实验

- 重启本机 Steam 后 CreateLobby 恢复成功。
- 第二大厅：`109775242119301208`，本地 helper PID `36580`，owner `76561199012457364`。
- 远端执行：`join-lobby-hold 109775242119301208 30`。
- `LobbyEnter_t`：lobby ID 正确，response `1`。
- 加入快照：`member_count=2 valid_member_count=2 is_member=True`，成员为本地与远端两个 Steam ID。
- 第二大厅 helper 收到远端 `state=0x00000001`，30 秒后收到 `state=0x00000002`。
- 保持第二大厅期间连续 10 次查询：服务器均 `ONLINE`，玩家数均为 1，玩家名为 `Z。`。
- 远端诊断进程 `Keepalive complete seconds=30`、`LeaveLobby complete`、exit `0`。
- 离开第二大厅后：`ONLINE;players=1`，玩家连接时长增长到 593.3 秒。

### 第二大厅结论

- 已证明：本地软件可以创建一个新的 Steam lobby，让已经连接 24561 的远端账号加入和离开该新大厅，而不影响现有游戏服务器连接。
- Steam lobby membership 可以同时或先后变化，已经建立的 Source 网络连接不会自动跟随这些变化关闭。
- 远端直接 `CreateLobby` 仍可能被 Steam result 25 限流；这属于创建配额问题，不是连接耦合问题。

## 2026-08-15 真实 reservation cookie 的 lobby 只读检查

### 输入与服务器证据

真实 L4D2 客户端连接 `sv_allow_lobby_connect_only=1` 服务器后，服务器 status 为：

```text
players : 3 humans, 0 bots (12 max) (not hibernating) (reserved 186000047bb2ba4)
```

- cookie hex：`0x0186000047BB2BA4`。
- lobby decimal：`109775242120604580`。
- 两者为同一个 64 位值。

### 只读方法

- 新增：`research/SteamLobbyProbe/Inspect-LobbyDiagnostic.ps1`。
- x86 Steam API：`D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll`。
- 请求 `RequestLobbyData` 后泵回调约 2.5 秒。
- 只调用 Request/Get API；静态导入列表中没有 Set、Join、Leave 或 owner 修改 API。
- 对同一 lobby 最初连续读取两次，结果一致；后续成员加入导致成员和对抗动态字段更新。

### 完整结果

```text
RequestLobbyData=True
Owner=76561199012457364
MemberCount=1
Member[0]=76561199012457364 owner=True
MetadataCount=28
Metadata[0] key="Game:campaign" value="L4D2C2"
Metadata[1] key="Game:chapter" value="1"
Metadata[2] key="Game:difficulty" value="normal"
Metadata[3] key="Game:dlcrequired" value="0"
Metadata[4] key="Game:maxrounds" value="3"
Metadata[5] key="Game:MissionInfo:addon" value="0"
Metadata[6] key="Game:MissionInfo:Author" value="Valve"
Metadata[7] key="Game:MissionInfo:builtin" value="1"
Metadata[8] key="Game:MissionInfo:DisplayTitle" value="#L4D360UI_CampaignName_C2"
Metadata[9] key="Game:MissionInfo:InfectedOnly" value="0"
Metadata[10] key="Game:MissionInfo:MissionFile" value="missions/campaign2.txt"
Metadata[11] key="Game:MissionInfo:SurvivorSet" value="2"
Metadata[12] key="Game:MissionInfo:Version" value="1"
Metadata[13] key="Game:MissionInfo:Website" value="http://store.steampowered.com"
Metadata[14] key="Game:MissionInfo:workshopid" value="0"
Metadata[15] key="Game:Mode" value="versus"
Metadata[16] key="Game:ModeInfo:addon" value="0"
Metadata[17] key="Game:ModeInfo:workshopid" value="0"
Metadata[18] key="Game:sk_versus" value="35"
Metadata[19] key="Game:state" value="game"
Metadata[20] key="Game:vanilla" value="1"
Metadata[21] key="Members:numMachines" value="1"
Metadata[22] key="Members:numPlayers" value="1"
Metadata[23] key="Members:numSlots" value="8"
Metadata[24] key="Options:Server" value="official"
Metadata[25] key="System:access" value="public"
Metadata[26] key="System:lock" value=""
Metadata[27] key="System:network" value="LIVE"
GetLobbyGameServer=False ip=0.0.0.0 port=0 steam_server_id=0
```

### 后续动态状态复核

最终校验时 lobby 新增一个成员，得到：

```text
Owner=76561199012457364
MemberCount=2
Member[0]=76561199012457364 owner=True
Member[1]=76561198680619771 owner=False
MetadataCount=28
Metadata[18] key="Game:sk_versus" value="37"
Metadata[21] key="Members:numMachines" value="2"
Metadata[22] key="Members:numPlayers" value="2"
GetLobbyGameServer=False ip=0.0.0.0 port=0 steam_server_id=0
```

除这些动态值外，metadata 键集合及其他值保持不变，仍没有任何 `Server:*` 键。服务器 status 的 3 名玩家与最新 lobby 的 2 名成员不是同步采样，中间有玩家临时进出；这组人数不能用于判断服务器玩家集合与 Steam lobby 成员集合是否一致。

### 分层结论

- **Steam lobby metadata 层**：没有 `server:reservationid`、`server:connectstring`、`server:adronline` 或其他 `Server:*` 字段。
- **Steam game-server binding 层**：`GetLobbyGameServer=False`，没有公开绑定的 IP/端口/server SteamID。
- **L4D2 MatchFramework/网络层**：`Game:state=game` 且服务器明确显示该 cookie 已 reserved，说明真实客户端在普通 metadata 之外完成了 reservation 请求。
- **服务器权威层**：服务器持有的 cookie 就是 lobby CSteamID；这是 reservation 是否成功的直接证据。

因此，正确 `reservationid` 的数值格式已经确定：使用真实 lobby CSteamID。尚需复现的是让 `sv_allow_lobby_connect_only=1` 服务器接受该 cookie 的游戏级 reservation 请求，而不是继续向普通 lobby metadata 写入同名字段。真实服务器地址也不在本次可见 metadata 或 `GetLobbyGameServer` 中，仍应从 L4D2 MatchFramework 会话/加入回复路径获取或传递。
