# 基线读取集

- `research/SteamLobbyProbe/Program.cs`：现有 Steam API 动态绑定、ManualDispatch、创建/读写/离开 lobby 的唯一实现所有者。
- `research/SteamLobbyProbe/README.md`：现有中文运行说明和已验证边界。
- `research/SteamLobbyProbe/SteamLobbyProbe.csproj`：x86、.NET 8 构建约束。
- `research/NativeMatchFrameworkProbe/README.zh-CN.md`：外部 MatchFramework 探测结果和 `RunFrame` 限制。
- L4D2 `matchmaking.dll` 逆向证据：`server:connectstring` 被格式化为 `connect %s`；`server:reservationid` 为零时回退到 64 位 session/lobby ID。
- Steamworks `ISteamMatchmaking::SetLobbyGameServer` 文档：IP 和端口按主机序传入；IP/端口或 Steam server ID 至少一组有效；调用后触发 `LobbyGameCreated_t`。
- Steamworks lobby invite 文档：游戏未运行时通过 `+connect_lobby <64-bit lobby ID>` 启动。

## 已验证环境

- Steam 进程运行中，当前 Steam ID 为 `76561199012457364`。
- `D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll` 存在。
- 现有 probe 在不启动 L4D2 的情况下可创建 AppID 550 lobby、写入并读回九个字段，然后正常离开。
- `dotnet build -c Release`：0 warning，0 error。
