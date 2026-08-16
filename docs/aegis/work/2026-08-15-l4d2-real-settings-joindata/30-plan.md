# Real L4D2 Settings and JoinData Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用真实 reserved lobby 的 typed Settings 完成带 settings 的服务器 reservation，并通过同一模板生成可供真实 L4D2 客户端加入的 ReplyJoinData。

**Architecture:** 提取共享 binary KeyValues 编解码器，新增真实 session settings profile。`ReservationProtocol` 继续拥有 UDP payload/ICE，`LobbyJoinProtocol` 继续拥有 Request/Reply 语义，`Program` 新增不影响旧模式的 `server-reserved` 生命周期。

**Tech Stack:** C#/.NET 8 x86、PowerShell、Steamworks lobby chat、Source connectionless UDP、ICE level 1、binary KeyValues。

**Baseline / Authority Refs:** `20-spec.md`、`../2026-08-15-l4d2-reservation-handshake/50-evidence.md`、`../2026-08-14-l4d2-server-lobby/50-evidence.md`、现有 `LobbyJoinProtocol.cs` 和 `ReservationProtocol.cs`。

**Compatibility Boundary:** 旧 `server`、`reserve-server`、`reservation-vector` 和 `protocol-reply` 的参数及默认行为保持可用；不写入 RCON 密码；不修改服务器配置；旧空 settings 固定向量保持不变。

**Verification:** `Test-RealSessionSettings.ps1`、`Test-ReservationProtocol.ps1`、`Test-LobbyJoinProtocol.ps1`、Release build、定向格式检查、授权服务器 status、真实客户端 console 和服务器玩家状态。

---

### Task 1: 共享 Binary KeyValues 所有者

**Files:**
- Create: `research/SteamLobbyProbe/BinaryKeyValues.cs`
- Create: `research/SteamLobbyProbe/RealSessionSettings.cs`
- Create: `research/SteamLobbyProbe/BinaryKeyValuesTestHelpers.ps1`
- Create: `research/SteamLobbyProbe/Test-RealSessionSettings.ps1`
- Modify: `research/SteamLobbyProbe/LobbyJoinProtocol.cs`

**Why this task exists:** reservation 和 ReplyJoinData 必须共享同一套类型、层级和序列化规则；继续保留 `LobbyJoinProtocol` 私有 writer 会形成两个协议所有者。

**Impact / Compatibility:** 新 writer 必须逐字节保持现有 legacy ReplyJoinData 固定向量；现有 parser 的 request 路径和身份检查不变。

**Repair Track:** canonical owner 改为 `BinaryKeyValues`；只移动通用类型、读写和索引逻辑，不改变 JoinData 业务规则。

**Retirement Track:** `LobbyJoinProtocol` 内的 `Type*` 常量、`KvEntry`、`Encode`、`WriteList`、`WriteCString`、整数 writer 和通用 parser 在新模块通过 legacy 回归后删除。

- [ ] **Step 1: 写真实 Settings RED 测试**

创建 `Test-RealSessionSettings.ps1`，发布 x86 程序并调用尚不存在的命令：

```powershell
$output = (& $probe real-settings-vector 0x12345678 0x0186000047CF0FD8 2243 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "real-settings-vector failed.`n$output"
}
```

使用独立 PowerShell parser 验证 `Settings/Game/Mode=versus`、`Settings/Game/MissionInfo/MissionFile`、
`Settings/Members/numSlots=8`、`Settings/Options/Server=official`、`Settings/System/network=LIVE`，并断言没有 `Settings/Server`。

- [ ] **Step 2: 运行 RED**

Run: `& .\Test-RealSessionSettings.ps1`

Expected: FAIL，因为 `real-settings-vector` 尚不存在并落入旧 CLI 路径。

- [ ] **Step 3: 实现通用 KeyValues 模型与严格 parser**

在 `BinaryKeyValues.cs` 定义：

```csharp
internal sealed record BinaryKvEntry(byte Type, string Name, object Value);

internal static class BinaryKeyValues
{
    internal static BinaryKvEntry Object(string name, params BinaryKvEntry[] children);
    internal static BinaryKvEntry String(string name, string value);
    internal static BinaryKvEntry Int32(string name, int value);
    internal static BinaryKvEntry UInt64(string name, ulong value);
    internal static byte[] Encode(params BinaryKvEntry[] entries);
    internal static bool TryIndex(
        ReadOnlySpan<byte> data,
        int offset,
        out Dictionary<string, object> index,
        out int bytesConsumed);
}
```

整数和 UInt64 继续使用大端；parser 必须消费完整列表并返回 `bytesConsumed`，调用者负责检查尾随数据。

- [ ] **Step 4: 实现真实模板**

在 `RealSessionSettings.cs` 定义：

```csharp
internal static class RealSessionSettings
{
    internal const int NumSlots = 8;
    internal static BinaryKvEntry CreateReservationSettings();
    internal static byte[] EncodeReservationSettings();
    internal static Dictionary<string, string> CreateLobbyMetadata();
}
```

`CreateReservationSettings()` 必须逐项实现 `20-spec.md` 的 28 字段 typed tree；`CreateLobbyMetadata()` 输出同一数据的 Steam 字符串视图。

- [ ] **Step 5: 迁移 LobbyJoinProtocol 到共享编解码器**

将 request parser 改为：

```csharp
if (!BinaryKeyValues.TryIndex(request, 4, out index, out var consumed)
    || consumed != request.Length - 4)
{
    return false;
}
```

legacy Reply 构造继续使用原有精简字段，但通过 `BinaryKeyValues.Encode` 写出，保证旧测试不变。

- [ ] **Step 6: 运行 GREEN 与 legacy 回归**

Run:

```powershell
& .\Test-RealSessionSettings.ps1
& .\Test-LobbyJoinProtocol.ps1
```

Expected: 两个脚本 exit `0`；legacy ReplyJoinData encoding 仍通过。

### Task 2: 带真实 Settings 的 Reservation 固定向量

**Files:**
- Modify: `research/SteamLobbyProbe/ReservationCommand.cs`
- Modify: `research/SteamLobbyProbe/Test-RealSessionSettings.ps1`
- Modify: `research/SteamLobbyProbe/Test-ReservationProtocol.ps1`

**Why this task exists:** 需要证明 settings bytes 确实进入 reservation 明文，长度、补零和 ICE 加密没有被高层 profile 破坏。

**Impact / Compatibility:** `reservation-vector` 和 `reserve-server` 继续传空 settings；只新增 `real-settings-vector` 和可供 `Program` 调用的 typed reservation 方法。

- [ ] **Step 1: 扩展 RED 断言**

`Test-RealSessionSettings.ps1` 从命令输出读取：

```text
SettingsSize=<n>
SettingsRaw=<hex>
PlainPayload=<hex>
EncryptedPayload=<hex>
ReservationRequest=<hex>
```

断言 plaintext 偏移 `12..15` 的 little-endian length 等于 `SettingsSize`，偏移 `16` 开始与 `SettingsRaw` 完全一致，尾部仅允许 ICE 对齐零字节。

- [ ] **Step 2: 运行 RED**

Run: `& .\Test-RealSessionSettings.ps1`

Expected: FAIL，因为新命令尚未输出 reservation vector。

- [ ] **Step 3: 提取可复用 UDP reservation 方法**

在 `ReservationCommand` 增加：

```csharp
internal static int RunLive(
    IPEndPoint endpoint,
    ulong cookie,
    ReadOnlySpan<byte> settings,
    int timeoutMilliseconds,
    int hostVersion);
```

现有 CLI `RunLive(string[] args)` 解析参数后调用该方法并传 `[]`。新命令 `real-settings-vector` 调用
`RealSessionSettings.EncodeReservationSettings()` 和 `ReservationProtocol.CreateReservationRequest(...)`。

- [ ] **Step 4: 固定独立 ICE oracle**

使用临时 `ICECipher 1.0` 对 `challenge=0x12345678` 与真实 settings plaintext 生成 ciphertext；把精确 hex 固定进
`Test-RealSessionSettings.ps1`。临时依赖不得加入项目。

- [ ] **Step 5: 运行 GREEN 和空 settings 回归**

Run:

```powershell
& .\Test-RealSessionSettings.ps1
& .\Test-ReservationProtocol.ps1
```

Expected: 新向量通过，原 `04944D6A637033FE8CD590BB4FE70B99` 固定向量保持不变。

### Task 3: 真实模板 ReplyJoinData

**Files:**
- Modify: `research/SteamLobbyProbe/RealSessionSettings.cs`
- Modify: `research/SteamLobbyProbe/LobbyJoinProtocol.cs`
- Create: `research/SteamLobbyProbe/Test-RealLobbyJoinProtocol.ps1`
- Modify: `research/SteamLobbyProbe/Program.cs`

**Why this task exists:** 真实客户端需要同一 session profile、动态成员和实际 Server 节点，不能重放旧地址和旧 lobby ID。

**Impact / Compatibility:** 现有 `TryCreateReply` overload 和 `protocol-reply` 保持 legacy 输出；新增 real profile overload 和 `protocol-reply-real` 诊断命令。

- [ ] **Step 1: 写 real Reply RED 测试**

复用现有 254 字节 RequestJoinData fixture，调用：

```powershell
& $probe $SteamApiPath protocol-reply-real $requestHex `
    '202.105.108.88:27084' 109775242121908184 `
    76561199012457364
```

独立 parser 断言：真实 Game/MissionInfo/ModeInfo 字段存在、`Members/numMachines=2`、请求者 machine/player 身份正确、
`Server/connectstring=202.105.108.88:27084`、`Server/reservationid=0x0186000047CF0FD8`。

- [ ] **Step 2: 运行 RED**

Run: `& .\Test-RealLobbyJoinProtocol.ps1`

Expected: FAIL，因为 `protocol-reply-real` 尚未实现。

- [ ] **Step 3: 实现动态 member 模型和 real Reply**

在 `RealSessionSettings.cs` 增加：

```csharp
internal readonly record struct SessionMachine(
    ulong SteamId,
    string PlayerName,
    string TuVersion,
    ulong DlcMask);

internal static BinaryKvEntry CreateReplySettings(
    IReadOnlyList<SessionMachine> machines,
    string connectString,
    ulong lobbyId);
```

`LobbyJoinProtocol` 从 RequestJoinData 取得请求者字段，构造 owner/requester machines，然后编码
`SysSession::ReplyJoinData`。owner 与 requester 相同时只生成一台 machine。

- [ ] **Step 4: 增加 real CLI 路由**

`Program.Main` 在 legacy `protocol-reply` 旁增加 `protocol-reply-real`，两者共享参数解析，只改变 settings profile。

- [ ] **Step 5: 运行 GREEN 与相关回归**

Run:

```powershell
& .\Test-RealLobbyJoinProtocol.ps1
& .\Test-LobbyJoinProtocol.ps1
& .\Test-ReservationProtocol.ps1
```

Expected: 三个脚本 exit `0`。

### Task 4: `server-reserved` 生命周期与 Live 验收

**Files:**
- Modify: `research/SteamLobbyProbe/Program.cs`
- Modify: `research/SteamLobbyProbe/ReservationCommand.cs`
- Create: `research/SteamLobbyProbe/Test-ServerReservedProtocol.ps1`
- Modify: `research/SteamLobbyProbe/README.md`
- Create: `docs/aegis/work/2026-08-15-l4d2-real-settings-joindata/50-evidence.md`

**Why this task exists:** 把 lobby 创建、真实 metadata、带 settings reservation 和真实 ReplyJoinData 组合成可操作流程。

**Impact / Compatibility:** 新模式不影响旧 `server`；不调用 `SetLobbyGameServer`，不写 `Server:*` metadata；UDP timeout `8` 只表示需要外部 status 验证，不等于拒绝。

- [ ] **Step 1: 增加 CLI contract RED**

在本地测试中调用缺少 endpoint 的新模式并断言明确 usage：

```text
server-reserved mode requires an IPv4 endpoint in the form ip:port
```

Run: Release executable with `<steam_api.dll> server-reserved`.

Expected: RED，因为当前会把 `server-reserved` 当 lobby type。

- [ ] **Step 2: 实现模式解析和真实 metadata**

`Program.Main` 增加 `reservedServerMode`，复用 endpoint/lobbyType/keepalive 解析，固定 `Game:state=game`，并调用
`RealSessionSettings.CreateLobbyMetadata()`。旧 `serverMode` 分支保持原样。`ServerEndpoint` 明确定义网络地址转换：

```csharp
private readonly record struct ServerEndpoint(
    string ConnectString,
    IPAddress Address,
    uint HostOrderIp,
    ushort Port)
{
    internal IPEndPoint ToIPEndPoint() => new(Address, Port);
}
```

- [ ] **Step 3: 创建 lobby 后执行 reservation**

metadata Set/Get 后调用：

```csharp
var reservationExitCode = ReservationCommand.RunLive(
    endpoint.ToIPEndPoint(),
    result.LobbyId,
    RealSessionSettings.EncodeReservationSettings(),
    5000,
    ReservationProtocol.DefaultHostVersion);
```

退出码 `0` 或 `8` 时继续保持 lobby；其他退出码终止并 LeaveLobby。输出 lobby ID、settings size、JoinURI、
`verification=server_status_required` 边界。

- [ ] **Step 4: 将 callback 切换到 real Reply profile**

定义并使用显式 profile：

```csharp
internal enum LobbySettingsProfile
{
    Legacy,
    Real,
}
```

给 `ServerLobbyContext` 增加 `LobbySettingsProfile SettingsProfile`。旧 `server` 使用 `Legacy`，`server-reserved` 使用 `Real`；
`HandleLobbyChatMessage` 根据 context 调用对应 `TryCreateReply`。

- [ ] **Step 5: 本地完整验证**

Run:

```powershell
dotnet build -c Release
& .\Test-RealSessionSettings.ps1
& .\Test-RealLobbyJoinProtocol.ps1
& .\Test-ReservationProtocol.ps1
& .\Test-LobbyJoinProtocol.ps1
dotnet format .\SteamLobbyProbe.csproj --verify-no-changes `
  --include .\BinaryKeyValues.cs .\RealSessionSettings.cs .\LobbyJoinProtocol.cs `
  .\ReservationProtocol.cs .\ReservationCommand.cs
```

Expected: build `0` warning/error；四个协议脚本通过；定向格式通过。

- [ ] **Step 6: 授权服务器 live reservation**

前置只读 status 必须为 `0 humans` 和 `unreserved`。运行 `server-reserved 202.105.108.88:27084`，记录新 lobby ID、
challenge、settings size 和 request。随后只对该服务器读取 status，确认 `(reserved <same lobby id>)`。

- [ ] **Step 7: 真实客户端 JoinData 验收**

让真实 L4D2 客户端加入新 lobby。记录 helper 的 RequestJoinData/ReplyJoinData、客户端 console 的实际 endpoint、
服务器 status/A2S 玩家变化。成功标准是客户端不再输出 `failed to handle reservation request` 并进入目标服务器。

- [ ] **Step 8: 文档与证据收尾**

README 说明新命令、真实 profile、外部 status 顺序和残余风险。`50-evidence.md` 分开记录 reservation 接受与客户端入服，
不把其中一项替代另一项。
