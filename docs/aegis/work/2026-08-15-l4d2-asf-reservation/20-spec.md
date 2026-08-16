# ASF L4D2 Reservation 和 JoinData 设计

## 架构

将协议的纯 C# 部分提取为 `research/L4d2Protocol` 多目标库（`net8.0;net10.0`）。
`SteamLobbyProbe` 继续引用其 `net8.0` 目标，ASF 插件引用其 `net10.0` 目标。该库
拥有 binary KeyValues、真实 session profile、JoinData parser/builder、ICE 和异步
reservation client；它不依赖 Steam API、ASF 或控制台。

`L4d2AsfPlugin` 拥有配置、ASF bot 生命周期和 SteamKit2 transport。插件实现
`IBotConnection` 创建 lobby，并实现 `IBotSteamClient` 注册一个
`L4d2LobbyChatHandler`。handler 仅处理当前 host session 的 `EMsg.ClientChatMsg`：
它读取原始 payload、校验 `SysSession::RequestJoinData`，再以
`ClientMsg<MsgClientChatMsg>` 发送未加 NUL 的 Reply payload。

```text
ASF bot logged on
  -> validate plugin config
  -> create AppID 550 lobby
  -> write real metadata
  -> [optional] reserve endpoint with lobby CSteamID
  -> publish active session to custom chat handler

L4D2 client RequestJoinData
  -> L4d2LobbyChatHandler receives raw ClientChatMsg
  -> L4d2Protocol validates request and builds ReplyJoinData
  -> handler sends raw ClientChatMsg payload
  -> client connects to Server/connectstring
```

## 配置与安全默认值

插件从与 DLL 同目录的 `L4d2AsfPlugin.json` 读取配置。缺少文件、无 enabled bot、
endpoint 无效或配置校验失败时只记录日志，不创建大厅。

最小配置：

```json
{
  "EnabledBots": ["sirp"],
  "Endpoint": "106.54.197.3:24561",
  "ReservationEnabled": false,
  "LobbyType": "Public",
  "MaxMembers": 8
}
```

`ReservationEnabled=true` 时，插件用真实 settings 对同一 endpoint 发送 reservation。
若 UDP success response 超时，插件保留 lobby 并记录 `verification=server_status_required`；
它不把 timeout 解释为服务器拒绝，也不把它记录为已接受。

## 会话状态

每个 bot 同时最多有一个 active session。状态依次为 `Creating`、`Ready`、
`ReservationPending`、`ReservationResponseReceived`、`ReservationStatusRequired` 或
`Failed`。只有 `Ready`、`ReservationResponseReceived` 和
`ReservationStatusRequired` 会向有效的 RequestJoinData 发送 reply。所有其他原始聊天
消息、其他 lobby 和身份不一致的请求均被忽略并以摘要日志记录，不写原始玩家 payload。

public lobby 的 metadata 来自 `RealSessionSettings.CreateLobbyMetadata()`；不写
`server:*`，也不调用 `SetLobbyGameServer`。真正的服务器地址和 reservation ID 仅出现于
ReplyJoinData 的 `Server` 节点，分别为配置 endpoint 和当前 lobby CSteamID。

## 测试与验收

本地自动化：

- 配置验证：禁用默认值、bot 白名单、IPv4 endpoint、成员上限和 reservation timeout。
- real metadata 和 reservation settings 保持原 `SteamLobbyProbe` 的 typed profile。
- Request fixture 产生的 Reply payload 与原 helper 的字段、身份、地址和 cookie 一致，且
  transport payload 不添加 NUL。
- 本地 UDP fixture 验证 challenge、reservation packet、accepted/rejected/timeout 路径。
- `SteamLobbyProbe` 现有协议 PowerShell 回归和 ASF 插件的 `net10.0` 测试均通过。

live 验收分两阶段：

1. 部署 `ReservationEnabled=false`、endpoint `106.54.197.3:24561`。真实客户端通过
   lobby URI 加入，ASF 日志记录有效 Request/Reply，客户端和服务器均显示连接成功。
2. 部署 `ReservationEnabled=true`、endpoint `202.105.108.88:27083`。RCON/status 首先
   证明服务器 `reserved <lobby-cookie>`；随后真实客户端通过 lobby URI 入服，服务器
   status 显示该玩家 active。

## 兼容与退休边界

- 现有 `SteamLobbyProbe` 的 CLI 和 PowerShell 诊断路径保留，改为使用共享协议库，
  保持固定向量和既有参数语义。
- 当前 20 秒 ASF probe 行为被配置驱动的 host session 替代；没有配置时不做任何 Steam
  mutation，消除重启后意外创建测试大厅的行为。
- 不保留私有反射、字符串聊天 API 或 `server:*` metadata 作为 ReplyJoinData 的备选实现。
