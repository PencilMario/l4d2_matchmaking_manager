# 基线读取集

- `research/SteamLobbyProbe/LobbyJoinProtocol.cs`：现有 RequestJoinData parser、ReplyJoinData builder 和 binary KeyValues writer。
- `research/SteamLobbyProbe/ReservationProtocol.cs`：reservation payload、ICE 和 packet 所有者。
- `research/SteamLobbyProbe/ReservationCommand.cs`：独立 UDP reservation CLI 和超时语义。
- `research/SteamLobbyProbe/Program.cs`：Steam lobby 生命周期、server helper 和 chat callback 所有者。
- `research/SteamLobbyProbe/Test-LobbyJoinProtocol.ps1`：已验证的 ReplyJoinData 编码契约。
- `research/SteamLobbyProbe/Test-ReservationProtocol.ps1`：ICE、reservation packet 和本地 UDP fixture。
- `docs/aegis/work/2026-08-14-l4d2-server-lobby/50-evidence.md`：真实客户端入服对照和真实 reserved lobby metadata。
- `docs/aegis/work/2026-08-15-l4d2-reservation-handshake/50-evidence.md`：服务器接受 cookie 的 UDP reservation 证据。

## 真实模板来源

真实 reserved lobby `0x0186000047BB2BA4` 的只读快照包含 28 个 metadata 字段：

- `Game`：campaign、chapter、difficulty、DLC、MissionInfo、Mode、ModeInfo、state、vanilla 等。
- `Members`：`numMachines=1`、`numPlayers=1`、`numSlots=8`。
- `Options`：`Server=official`。
- `System`：`access=public`、`lock=`、`network=LIVE`。

该快照没有任何 `Server:*` metadata，也没有公开 game-server binding。服务器地址和 reservation cookie 必须由
helper 在 ReplyJoinData 中补充。

## 已确认编码事实

- Lobby chat 的 binary KeyValues 使用类型字节、NUL 结尾 UTF-8 名称/字符串、大端整数和 `0x0B` 列表结束符。
- `RequestJoinData/Settings/Members/machine0` 提供请求者 SteamID、玩家名、TU version 和 DLC mask。
- reservation 明文布局为 magic、cookie、settings length、settings bytes，再按 8 字节补零并 ICE 加密。
