# 基线读取集

- `research/SteamLobbyProbe/BinaryKeyValues.cs`：binary KeyValues 读写和大/小端契约。
- `research/SteamLobbyProbe/RealSessionSettings.cs`：真实 28 字段 metadata、reservation
  settings 和 ReplyJoinData 的 `Server` 节点。
- `research/SteamLobbyProbe/LobbyJoinProtocol.cs`：请求身份校验和 ReplyJoinData 生成。
- `research/SteamLobbyProbe/ReservationProtocol.cs`：challenge、ICE 和 reservation packet。
- `research/SteamLobbyProbe/ReservationCommand.cs`：现有 UDP 生命周期及超时语义。
- `docs/aegis/work/2026-08-15-l4d2-real-settings-joindata/50-evidence.md`：真实客户端
  入服、reservation 和 owner 离开的既有证据。
- `research/L4d2AsfPlugin/L4d2LobbyProbePlugin.cs`：当前 ASF lobby 生命周期 PoC。
- ASF 6.3.8.4 `Plugins/Interfaces/IBotSteamClient.cs`：公开 custom handler 注册点。
- SteamKit2 `ClientMsgHandler`、`ClientMsg<MsgClientChatMsg>`：原始 lobby chat 收发边界。

## 已确认事实

- `IBotSteamClient.OnBotSteamHandlersInit()` 返回的 handler 由 ASF 加入 bot 的
  `SteamClient`，handler 的受保护 `Client` 可调用 `Send()`。
- `ClientMsg<MsgClientChatMsg>` 的 payload 直接写入网络消息；标准
  `SteamFriends.SendChatRoomMessage()` 会追加字符串 NUL，不能用于 ReplyJoinData。
- 现有 helper 已向真实 L4D2 客户端发送过 631 字节 ReplyJoinData，并在 reservation
  被服务器接受后验证客户端入服。
