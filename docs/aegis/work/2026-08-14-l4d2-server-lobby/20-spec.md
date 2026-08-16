# 大厅成员关系与游戏连接解耦实验

## 目标

验证远端真实 L4D2 已通过 helper-owned lobby 进入 `106.54.197.3:24561` 后，以下 Steam lobby 操作是否会让客户端离开游戏服务器：

1. 同一远端 Steam 账号通过独立交互式进程创建并保持第二个私人大厅。
2. 同一远端 Steam 账号通过独立交互式进程调用原大厅的 `LeaveLobby`。

实验不转让原大厅 owner；本地 helper 在全过程继续拥有并响应原大厅。

## 设计

- 新增 `create-lobby-hold [type] [seconds]`。它沿用现有创建和回调路径，输出新大厅 ID，并在指定时间内只泵 Steam 回调。
- 新增 `leave-lobby <lobbyId> [settleSeconds]`。它列出操作前成员，调用 `ISteamMatchmaking::LeaveLobby`，泵回调后再次列出成员。
- 若远端 `CreateLobby` 被 Steam 限流，则由本地软件创建第二大厅，远端通过 `join-lobby-hold <lobbyId> [seconds]` 加入并保持；该路径仍直接验证远端账号进入第二大厅时游戏连接是否保留。
- 两个命令都使用 AppID 550 的 x86 `steam_api.dll`，通过远端交互式计划任务运行，避免 SSH Session 0 中 `SteamAPI_Init=false`。
- 原 server helper 是原大厅 metadata、ReplyJoinData 和生命周期的唯一所有者；诊断进程不写原大厅 metadata，不调用 `SetLobbyOwner`。

## 实验顺序

1. 从头启动本地 `server ... game` helper。
2. 启动远端 L4D2 并用 `+connect_lobby` 加入，等待 A2S 玩家数为 1。
3. 运行 `create-lobby-hold private 30`，检查新大厅 ID、原大厅成员、远端 PID、客户端日志和 A2S。
4. 清理并从头建立第二次基线。
5. 运行 `leave-lobby <originalLobbyId> 3`，检查原大厅 `LobbyChatUpdate state=0x2`、远端 PID、客户端日志和 A2S。

## 成功判定

- “保持服务器连接”必须同时满足：远端 `left4dead2.exe` 仍运行、A2S 玩家数仍为 1、客户端日志未出现返回大厅或断开记录。
- 仅有 PID 存活不足以证明仍在服务器。
- 创建新大厅和离开原大厅分别记录结论，不能用其中一个结果替代另一个。

## 兼容边界

- 现有普通 probe、`server`、`protocol-reply`、`transfer-owner` 命令保持可用。
- 不修改目标服务器配置，不注入 L4D2 进程，不调用完整 `MatchFramework::RunFrame`。
- `transfer-owner` 保留为历史诊断工具，但不再用于本实验主路径。
