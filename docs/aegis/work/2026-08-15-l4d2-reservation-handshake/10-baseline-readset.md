# 基线读取集

- `research/SteamLobbyProbe/Program.cs`：现有 CLI 和 Steam lobby 行为的所有者。
- `research/SteamLobbyProbe/SteamLobbyProbe.csproj`：.NET 8、x86 构建约束。
- `research/SteamLobbyProbe/README.md`：中文运行说明和已验证边界。
- `docs/aegis/work/2026-08-14-l4d2-server-lobby/50-evidence.md`：真实 cookie 与 metadata 分层证据。
- L4D2 `engine_srv.so` 反编译：`CBaseServer::ProcessConnectionlessPacket`、
  `ReplyChallenge`、`ReplyReservationRequest`、`SendReservationStatus`。
- Kisak-Strike 同源文件：`common/proto_oob.h`、`engine/baseclientstate.cpp`、
  `engine/baseserver.cpp`、`mathlib/IceKey.cpp`。
- 独立 ICE oracle：临时安装的 PyPI `ICECipher 1.0`，不作为项目依赖。

## 已确认协议值

- `A2S_GETCHALLENGE='q'`，即 `0x71`。
- `A2S_RESERVE='n'`，即 `0x6E`。
- `S2C_CHALLENGE='A'`，即 `0x41`。
- `S2A_RESERVE_RESPONSE='p'`，即 `0x70`。
- 当前服务器 `GetHostVersion()=2243`，小端字节为 `C3 08 00 00`。
- 最小明文 payload 为 `magic:uint32 + cookie:uint64 + settingsLength:int32`，共 16 字节。
