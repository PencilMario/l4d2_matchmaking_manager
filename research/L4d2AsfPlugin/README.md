# ASF L4D2 Lobby Host

这是一个 ArchiSteamFarm (ASF) `6.3.8.4` 插件。它让已登录的 ASF bot 成为 L4D2
（AppID `550`）Steam lobby 的 owner，不依赖 Steam Desktop Client、L4D2 客户端或
`steam_api.dll` 来创建和维护大厅。

插件只使用 ASF 的公开 `IBotConnection` 和 `IBotSteamClient` 扩展点，以及 SteamKit2
公开的 raw message handler。它不会修改 ASF 本体、使用反射访问私有成员，或写入
`server:*` lobby metadata。

## 行为

插件默认禁用。只有 `L4d2AsfPlugin.json` 存在且 bot 名在 `EnabledBots` 中时，bot 登录后才会：

1. 创建指定类型的 L4D2 lobby。
2. 写入真实 L4D2 session profile 的 28 个 metadata 字段，例如 `Game:Mode=versus` 和
   `Game:state=game`。
3. 发布该 bot 的 active session，接收二进制 `SysSession::RequestJoinData`，并用不附加 NUL
   的 raw `ClientChatMsg` 回复 `SysSession::ReplyJoinData`。
4. 当 `ReservationEnabled=true` 时，使用大厅 CSteamID 作为 cookie，向目标 IPv4 endpoint
   发送 reservation UDP 请求。

JoinData reply 中的 `Server/connectstring` 和 `Server/reservationid` 分别来自 JSON endpoint
和新建 lobby ID。它们不作为普通 Steam lobby metadata 写入。

## 配置

在 DLL 同目录新建 `L4d2AsfPlugin.json`：

```json
{
  "EnabledBots": ["sirp"],
  "Endpoint": "106.54.197.3:24561",
  "ReservationEnabled": false,
  "LobbyType": "Public",
  "MaxMembers": 8,
  "ReservationTimeoutMilliseconds": 5000,
  "HostVersion": 2243
}
```

- `EnabledBots`：大小写不敏感的 ASF bot 名白名单。缺失或空数组时插件无副作用。
- `Endpoint`：必须是 `IPv4:port`；不接受域名或 IPv6。
- `LobbyType`：SteamKit2 的 `ELobbyType`，通常使用 `Public`。
- `MaxMembers`：`1` 到 `8`。
- `ReservationEnabled`：`false` 用于不消费 lobby reservation 的对照服务器；`true` 用于
  `sv_allow_lobby_connect_only=1` 的 reservation 路径。
- `ReservationTimeoutMilliseconds` 和 `HostVersion` 仅影响 reservation；默认分别为 5000 和
  2243。

reservation 超时不会被记为成功或拒绝。大厅保持在
`ReservationStatusRequired`，日志会出现 `verification=server_status_required`，此时必须通过
授权的服务器 status/RCON 观察相同 cookie 后再让客户端加入。

## 构建

```powershell
dotnet test .\research\L4d2AsfPlugin\tests\L4d2AsfPlugin.Tests.csproj --nologo

dotnet build .\research\L4d2AsfPlugin\L4d2AsfPlugin.csproj -c Release --nologo `
  -p:ASFSourcePath='C:\path\to\ArchiSteamFarm-6.3.8.4-source'
```

`ASFSourcePath` 必须包含 `ArchiSteamFarm/ArchiSteamFarm.csproj`。插件和 ASF 都以 `net10.0`
构建。

## 部署与回滚

将 Release 输出中的以下文件放入独立目录，例如
`plugins/L4d2AsfPlugin/`：

```text
L4d2AsfPlugin.dll
L4d2AsfPlugin.deps.json
L4d2Protocol.dll
L4d2AsfPlugin.json
```

重启 ASF 后，从日志记录 `lobby=<id>` 和 `join_uri=steam://joinlobby/550/...`。使用真实 L4D2
客户端打开该 URI 才能验证游戏连接；ASF bot 本身不是游戏客户端。

回滚时删除这个独立插件目录并重启 ASF，或将 `EnabledBots` 设为空。不会改动 ASF 本体、bot
配置或游戏服务器设置。

## 当前验证边界

本地已验证配置、真实 metadata profile、session state、raw payload 不追加 NUL，以及针对 ASF
`6.3.8.4` 的 Release 编译。Docker 实测还确认了一项运行前提：bot owner 账号不能同时在 Steam
Desktop 或其他 Steam 游戏会话中登录。并发登录时，Steam 仍允许 ASF 创建且可读取 lobby metadata，
但其他账号的 `JoinLobby` 会收到 `LobbyEnter response=2`；关闭并发会话、重启 ASF 后，同一 Edge
客户端对新 lobby 收到 `response=1`。

`response=1` 只表示 Steam lobby 成员关系成立，不等同于 Source 游戏连接。目前真实 L4D2 经 URI
启动后尚未观测到 `RequestJoinData`，因此仍需验证 bot 的 AppID 550 游戏运行状态和实际
MatchFramework 会话发布。当前构建本身不等同于客户端已进入 reservation 服务器的 live 验收。
