# 任务意图

## 目标

将已在 `research/SteamLobbyProbe` 验证的 L4D2 reservation、typed KeyValues
和 `SysSession::ReplyJoinData` 协议移植到 ASF 插件。ASF 已登录 bot 应能作为
L4D2 lobby owner，向真实 L4D2 客户端提供可连接的 session 数据。

## 已批准范围

- 以 ASF `IBotSteamClient` 的公开扩展点注册自定义 SteamKit2 handler。
- 创建 AppID `550` public lobby，写入真实 28 字段 session metadata。
- 对指定 IPv4 endpoint 发送带真实 settings 的 UDP reservation，cookie 使用新 lobby
  的 CSteamID。
- 从原始 `EMsg.ClientChatMsg` 接收 `RequestJoinData`，并发送精确的二进制
  `ReplyJoinData`；payload 不附加 NUL。
- 先在 `106.54.197.3:24561` 验证 lobby chat transport 和客户端连接，再在
  `202.105.108.88:27083` 验证 reservation 服务器路径。

## 非目标

- 不修改 ASF 本体，不使用反射访问 ASF 私有字段。
- 不保存或输出 RCON 密码，不改游戏服务器配置。
- 不把普通 lobby metadata 当作 reservation 的替代。
- 不自动转移 lobby owner 或实现完整 MatchFramework。

## 风险

- `ClientChatMsg` 是 legacy struct message，错误的 payload 结尾字节会使 L4D2
  拒绝 JoinData。
- reservation UDP 的可达性依赖 ASF 容器所在主机的网络出口。
- 部分 L4D2 服务器接受 reservation 后不会即时返回成功包；此时 RCON/status 是
  唯一的接受证据。
