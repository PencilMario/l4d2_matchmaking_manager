# 证据记录

## 规划基线

- 真实 reserved lobby metadata：28 个字段，无 `Server:*`，`GetLobbyGameServer=False`。
- 现有 254 字节 RequestJoinData fixture 来自真实 L4D2 客户端。
- 现有 legacy ReplyJoinData 已在 `sv_allow_lobby_connect_only=0` 对照服务器驱动真实客户端入服。
- 最小空 settings reservation 已让 `202.105.108.88:27084` 接受 cookie。

下文按任务顺序记录 RED/GREEN、固定向量、live reservation 和真实客户端结果。

## Task 1-3 本地证据

2026-08-15 完成共享 binary KeyValues、真实 Settings 和 real Reply profile：

- `Test-RealSessionSettings.ps1`：Settings `551` 字节、28 个 typed leaf、无 `Settings/Server`；RequestJoinData 尾随字节被拒绝。
- `Test-RealSessionSettings.ps1`：带 Settings plaintext `568` 字节，Settings 长度和 reservation header 使用正确 little-endian；非空 ICE 密文固定 SHA-256 为 `7264B015B99764C412098DCCE5E00A3487B7DFCCD60B7C14E29328853429BC79`。
- `Test-ReservationProtocol.ps1`：旧空 Settings reservation 向量及 UDP fixture 全部通过。
- `Test-LobbyJoinProtocol.ps1`：legacy ReplyJoinData golden bytes 保持不变。
- `Test-RealLobbyJoinProtocol.ps1`：real Reply 使用真实 Game/MissionInfo/ModeInfo、`Members` 动态生成 owner/requester 两台 machine、`Options/System` 复用模板，并写入实际 `Server/connectstring` 与 `Server/reservationid`。
- `dotnet build -c Release --no-restore`：0 warning、0 error。

当前证据已扩展到授权服务器和真实客户端；27084 已验证 `server-reserved` 的完整 settings 与 real ReplyJoinData，27083 进一步验证了 reservation 必须从 edge 网络出口发送的情况。

## 27082 失败样本

本轮首先把目标切换到 `202.105.108.88:27082`：

- lobby：`109775242168041796`
- cookie：`0x018600004A8F0144`
- 本机 helper 能收到 challenge 并发送 551 字节真实 settings reservation packet。
- 发送后 RCON `status` 仍为 `unreserved`；edge 收到 `ReplyJoinData` 后，真实客户端输出 `Server error - failed to handle reservation request.`
- RCON 只读 cvar 显示 `sv_allow_lobby_connect_only=1`、`sv_force_unreserved=0`，所以不是配置明确关闭 lobby reservation。

结论：该轮不能把 lobby 层的 `ReplyJoinData sent=True` 当成入服成功；服务器是否保留 cookie 必须以 RCON 为准。

## 27083 edge 出口闭环

`202.105.108.88:27083` 的 RCON 在线，但本机发送标准 challenge 没有 UDP 回包。相同 challenge 从 edge 发送可得到：

```text
challenge = 0x02DD6E75
server    = 202.105.108.88:27083
```

使用当前 lobby cookie `0x018600004AADAF74`，本机 `real-settings-vector` 生成 551 字节 settings；edge 原样发送 581 字节 UDP reservation packet。服务器没有即时发送 response，但 RCON 立即显示：

```text
players : 0 humans, 0 bots (12 max) (not hibernating) (reserved 18600004aadaf74)
```

本次 lobby：

```text
lobby      = 109775242170052468
reservation= 0x018600004AADAF74
connect    = 202.105.108.88:27083
edge       = 76561199382197988
```

edge 使用 `steam://joinlobby/550/109775242170052468/76561199012457364` 加入后，helper 日志出现 edge 的 `RequestJoinData` 和 `ReplyJoinData sent=True`。RCON 随后显示：

```text
players : 1 humans, 0 bots (12 max) (not hibernating) (reserved 18600004aadaf74)
#  2 1 "Z。" STEAM_1:0:710966130 02:10 35 0 active 100000 58.55.184.63:27005
```

随后由 owner 账号调用 `leave-lobby`：

- 离开前：owner 与 edge 共 2 个有效 lobby member。
- 离开后：owner `is_member=False`；edge 仍保持服务器连接。
- 重新 `RequestLobbyData`：owner 自动变为 edge `76561199382197988`，`MemberCount=1`，`GetLobbyGameServer=True`。
- `server:connectstring=202.105.108.88:27083`、`server:reservationid=109775242170052468` 和游戏服务器地址 metadata 仍在。
- 最后一次 RCON 仍为 1 个 active human，且 reservation cookie 仍为 `18600004aadaf74`。

这证明：在本轮网络条件下，reservation packet 必须从 edge 出口发送；owner 离开 Steam lobby 不会自动清除已经建立的游戏连接或服务器 reservation。27083 这一轮使用了现有 `server` 模式的 legacy ReplyJoinData 来承载大厅回调，Server 字段和 cookie 正确；27084 的前一轮已额外验证过 `server-reserved` real ReplyJoinData。

## 27083 手动 IP 直连与 lobby 关联

为排除 lobby URI 的影响，新建独立 lobby 后不向 edge 发送 `steam://joinlobby`：

```text
lobby      = 109775242171763520
cookie     = 0x018600004AC7CB40
server     = 202.105.108.88:27083
RCON start = reserved 18600004ac7cb40
```

reservation 仍由 edge 发送完整 551 字节 settings packet；在 RCON 确认 cookie 已保留后，用户仅在 edge L4D2 控制台手动执行：

```text
connect 202.105.108.88:27083
```

结果：

- RCON：`1 humans`，edge `58.55.184.63:27005` 为 `active`，服务器仍保留 `18600004ac7cb40`。
- helper：收到 edge 的 `LobbyChatUpdate state=0x1`、`SysSession::RequestJoinData`，并记录 `ReplyJoinData sent=True`。
- 只读 `RequestLobbyData`：owner 为本机 `76561199012457364`，`MemberCount=2`，成员为 owner 和 edge `76561199382197988`。

结论限定为本轮条件：当目标服务器已经接受 lobby cookie 后，edge 的手动 IP 直连不但进入游戏服务器，也会触发对应 Steam lobby 的加入/JoinData 协议。本实验不证明未 reservation 的任意 IP 直连也会加入 lobby。
di

## 下载地区与大厅地域的边界

2026-08-15 对本机 Steam 客户端、L4D2 二进制和 Steamworks 文档的只读检查结果：

- 当前 Steam UI 的 `chunk~2dcc5aaf7.js` 将“下载地区”绑定到内部设置 `download_region`，设置号为 `8009`，类型为
  `int32`；候选值来自 `vecValidDownloadRegions` 的 `nRegionID`。用户更改下拉框时，UI 会显示“稍后重启”按钮，表明
  该设置需要重启 Steam 生效。
- `ISteamMatchmaking::AddRequestLobbyListDistanceFilter` 的官方描述为：距离基于用户 IP 地址和 Steam 后端的 IP
  location map。`Close=0`、`Default=1`、`Far=2`、`Worldwide=3`；官方接口未出现下载地区参数。
- 本机 `config.vdf` 未包含 `DownloadRegion`/`download_region`；其中的 `CurrentCellID`、`CellIDServerOverride` 是客户端
  CM 路由状态。`connection_log.txt` 显示 CM 列表请求带 `cellid=64`，`content_log*.txt` 显示 SteamPipe 内容源请求带
  `CellID 64`，说明 CM 与内容下载都使用 Cell 路由，但不能由此推出该值就是下载地区或 lobby 筛选条件。
- 当前 `matchmaking.dll` 存在 `SessionSearch`、`Filter=/Game:*`、`Near/*` 字符串；ASCII 与 UTF-16 字符串扫描均未发现
  `download_region`、`DownloadRegion` 或 `downloadregion`。这排除了该版本直接通过同名配置键读取下载地区；L4D2 可通过
  Steam API 获得地域行为，无须在游戏 DLL 内部读取该键。
- SteamKit 的 `SteamMatchmaking.GetLobbyList` 开源协议实现显示，`CMsgClientMMSGetLobbyList` 的 body 同时写入
  `cell_id = Client.CellID.Value` 和 `public_ip = Client.PublicIP`。因此 CellID 不是仅用于下载 CDN 的旁路状态，而是 Steam
  lobby 搜索请求的显式字段。
- 本机公网出口定位为中国青岛；但配置存在 `CellIDServerOverride=64`，连接日志中的 `GetCMListForConnect/?cellid=64`
  返回并优先连接 `lax1`，内容日志同一时段的 SteamCache 也均为 `*-lax1`。这与“下载地区为美国洛杉矶、匹配到美国玩家”
  的现场观察一致。

结论：下载地区能在 Steam 设置页面手动修改。下载地区对应的 Cell 会进入 Steam 的 lobby list 请求，并且本机的
`CellID=64 -> lax1` 取证与美国匹配现象一致。官方文档未公开服务端对 `cell_id` 与 `public_ip` 的加权/优先级，不能声称
二者哪个绝对覆盖另一个；但“下载地区不会影响 lobby 搜索”的结论已经被协议字段否定。验证强度应固定公网 IP、账号、搜索
条件和目标 lobby，切换下载地区并完整重启 Steam 后记录 CellID、CM 数据中心和 L4D2 返回集合及排序。不要把直接编辑
`CellIDServerOverride` 当作日常设置手段，它是原始路由覆盖项，适合受控诊断而非持久配置。

参考：<https://partner.steamgames.com/doc/api/ISteamMatchmaking#AddRequestLobbyListDistanceFilter>

协议实现参考：<https://github.com/SteamRE/SteamKit/blob/master/SteamKit2/SteamKit2/Steam/Handlers/SteamMatchmaking/SteamMatchmaking.cs>

## 24561 原 owner 离开后的 URI 重入

测试 lobby 为 `109775242179079768`，创建时记录：

```text
URI    = steam://joinlobby/550/109775242179079768/76561199012457364
server = 106.54.197.3:24561
state  = game
```

edge 先使用 URI 加入；probe 收到 edge `76561199382197988` 的 `RequestJoinData` 并输出 `ReplyJoinData sent=True`。
edge 的真实 `console.log` 随后确认 `Connected to 106.54.197.3:24561`，本机 A2S 为 1 个 human。

本机 owner `76561199012457364` 调用 `LeaveLobby` 后：

- 只读查询：owner 变为 edge，`MemberCount=1`；`game:state=game`、三个 `server:*` 地址/ID 字段和
  `GetLobbyGameServer=True ip=106.54.197.3 port=24561` 均保留。
- A2S 仍为 1 个 human，表明 edge 未因 owner 离开而退出 24561。

随后停止本机创建 probe，启动本机真实 L4D2 并仅重新调用原始 URI。12 秒后：

- A2S 为 `humans=2 max_players=12 bots=0`。
- 只读 lobby 查询：`MemberCount=2`；本机 `76561199012457364 owner=False`，edge
  `76561199382197988 owner=True`。
- `Members:numMachines=2`、`Members:numPlayers=2`，地址 metadata 与 `GetLobbyGameServer=True` 不变。

结论限定：`sv_allow_lobby_connect_only=0` 的 24561 上，旧 owner 离开后，带旧 owner SteamID 的 `steam://joinlobby`
URI 仍能让旧 owner 重新加入 lobby 并进入目标服务器。该 URI 的 owner 段不要求仍指向当前 lobby owner；但不能把该结果
外推到必须完成 reservation 的服务器。

随后再次分发同一原始 URI，用户确认本机真实客户端可正常进入。该人工确认与前述 A2S 和 lobby 成员读取相互印证。确认后，
用户主动退出了本机和 edge 的 L4D2 客户端；因此本轮不再将 24561 的当前玩家数或 Steam lobby 存活状态作为持续运行证据。
