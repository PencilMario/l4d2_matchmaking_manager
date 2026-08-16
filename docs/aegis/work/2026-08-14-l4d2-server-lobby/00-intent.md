# 任务意图

## 目标

让真实 L4D2 客户端通过 Steam lobby 加入 `202.105.108.88:27084`，并验证 lobby 使用以下权威值：

- `server:reservationid=<lobby Steam ID>`
- `server:connectstring=202.105.108.88:27084`

## 范围

- 扩展 `research/SteamLobbyProbe` 的命令行，增加 server-lobby 模式。
- 创建 AppID 550 lobby，写入 L4D2 匹配字段。
- 使用 `SetLobbyGameServer` 绑定目标 dedicated server，并用 `GetLobbyGameServer` 读回验证。
- 保持 helper 与 lobby 存活，输出 `steam://joinlobby` URI 和 `+connect_lobby` 参数。
- 启动真实 L4D2，收集客户端控制台日志与服务器 A2S 状态作为端到端证据。
- 更新中文说明文档。

## 非目标

- 不修改远程服务器配置。
- 不伪造 Steam Game Server ID。
- 第一阶段不手写 L4D2 connectionless reservation 数据包；只有真实客户端表明服务器拒绝 reservation cookie 时才进入该路径。

## 风险

- Steam lobby ID 是 MatchFramework 的 reservation cookie 回退值，但远程服务器未必已被同一 cookie 预订。
- helper 与 L4D2 使用同一 Steam 用户，需实测 L4D2 对“用户已在该 lobby 中”的处理。
- 远程服务器可能启用了 `sv_allow_lobby_connect_only` 或第三方插件限制。

## 2026-08-15 后续目标

- 不转让原大厅 owner。
- 远端真实客户端进入 `106.54.197.3:24561` 后，分别验证创建第二大厅与离开原大厅是否会中断游戏服务器连接。
- 以 A2S、客户端日志、进程状态和 helper 的 lobby callback 作为联合证据。
