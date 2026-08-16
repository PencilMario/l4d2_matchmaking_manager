# L4D2 Server Lobby 握手 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 创建绑定到指定 L4D2 dedicated server 的 Steam lobby，并让真实客户端通过 `+connect_lobby` 加入游戏。

**Architecture:** 保留现有 probe 模式，新增 `<steam_api.dll> server <ip:port> [lobbyType] [keepaliveSeconds]` 模式。SteamLobbyProbe 是 lobby metadata、game-server binding 和生命周期的唯一所有者；L4D2 本体仍是 MatchFramework `RunFrame`、reservation cookie 发送和最终 `connect` 命令的唯一所有者。

**Tech Stack:** .NET 8 x86、Steamworks flat C API、PowerShell 集成测试、L4D2 Source engine。

**Baseline / Authority Refs:** `00-intent.md`、`10-baseline-readset.md`、Steamworks `ISteamMatchmaking::SetLobbyGameServer`、L4D2 `matchmaking.dll` 逆向证据。

**Compatibility Boundary:** 现有 `<dll> [private|friends|public|invisible] [direct]` probe 调用保持可用；不在 helper 中调用完整 `MatchFramework::RunFrame`；不声称 `SetLobbyGameServer` 等价于远程服务器 reservation 成功。

**Verification:** `Test-ServerLobby.ps1` 红绿循环、Release build、短时真实 Steam lobby 读回、L4D2 `+connect_lobby` 端到端日志及 A2S 状态。

---

### Task 1: Server-lobby 集成测试

**Files:**
- Create: `research/SteamLobbyProbe/Test-ServerLobby.ps1`

**Why this task exists:** 固定 CLI、metadata、Steam game-server binding 和 join 参数的可观察契约。

**Impact / Compatibility:** 测试只创建短时 private lobby，完成后 helper 必须 LeaveLobby；不启动 L4D2。

**Verification:** `powershell -ExecutionPolicy Bypass -File .\Test-ServerLobby.ps1` 首次因缺少 `server` 模式失败，实施后 exit 0。

- [ ] 编写测试，运行 `dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o publish-test`。
- [ ] 执行 `publish-test\SteamLobbyProbe.exe <dll> server 202.105.108.88:27084 private 1`。
- [ ] 从输出提取 `lobby_id` 和 owner Steam ID，并断言：`server:reservationid` 等于 lobby ID、`server:connectstring` 等于目标、Set/GetLobbyGameServer 的 IP/端口一致、join URI 与 `+connect_lobby` 一致、进程退出时 LeaveLobby。
- [ ] 运行测试并确认它因现有程序把 `server` 当 lobby type 而失败。

### Task 2: Server-lobby 模式

**Files:**
- Modify: `research/SteamLobbyProbe/Program.cs`

**Why this task exists:** 让 helper 成为 lobby 字段、服务器绑定和 keepalive 的单一所有者。

**Impact / Compatibility:** 旧 probe 分支保持原行为；server 分支默认 `public`、8 slots、`versus`、`c2m1_highway`。

**Verification:** Task 1 测试转绿，随后运行旧 probe 命令确认兼容。

- [ ] 增加 server 参数解析：

```csharp
var serverMode = args.Length > 1 && string.Equals(args[1], "server", StringComparison.OrdinalIgnoreCase);
var endpoint = serverMode ? ParseServerEndpoint(args[2]) : default;
var lobbyType = serverMode
    ? (args.Length > 3 ? ParseLobbyType(args[3]) : LobbyTypePublic)
    : (args.Length > 1 ? ParseLobbyType(args[1]) : LobbyTypePrivate);
var keepaliveSeconds = serverMode && args.Length > 4 ? ParseKeepaliveSeconds(args[4]) : 0;
```

- [ ] server lobby 创建后写入固定字段，其中动态字段为：

```csharp
["server:connectstring"] = endpoint.ConnectString,
["server:reservationid"] = result.LobbyId.ToString(CultureInfo.InvariantCulture),
["options:server"] = "dedicated",
```

- [ ] 绑定并读回服务器：

```csharp
api.SetLobbyGameServer(matchmaking, result.LobbyId, endpoint.HostOrderIp, endpoint.Port, 0);
var bound = api.GetLobbyGameServer(matchmaking, result.LobbyId, out var ip, out var port, out var serverId);
```

- [ ] 输出：

```text
JoinURI=steam://joinlobby/550/<lobbyId>/<ownerSteamId>
ConnectLobbyArg=+connect_lobby <lobbyId>
```

- [ ] 使用 ManualDispatch 每 50ms pump callbacks，保持指定秒数；负数表示持续到 Ctrl+C。退出 finally 前 LeaveLobby。
- [ ] 增加 flat API delegates：

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void SteamApiSetLobbyGameServer(nint self, ulong lobbyId, uint ip, ushort port, ulong serverId);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate byte SteamApiGetLobbyGameServer(nint self, ulong lobbyId, out uint ip, out ushort port, out ulong serverId);
```

- [ ] 运行 Task 1 测试，确认转绿。
- [ ] 运行旧命令 `.\publish-test\SteamLobbyProbe.exe <dll> private`，确认旧 metadata 与 LeaveLobby 行为不变。

### Task 3: 真实客户端加入

**Files:**
- Create: `research/SteamLobbyProbe/Start-L4D2ServerLobbyTest.ps1`
- Modify: `research/SteamLobbyProbe/README.md`
- Modify: `docs/aegis/work/2026-08-14-l4d2-server-lobby/50-evidence.md`

**Why this task exists:** 用户目标是客户端真正加入，而不是只创建格式正确的 lobby。

**Impact / Compatibility:** 脚本只启动本机 helper 与 L4D2；不修改远程服务器，不持久化代理或 Steam 启动参数。

**Verification:** L4D2 控制台日志出现 lobby ID、目标地址和成功连接状态；A2S 查询显示客户端加入后的玩家数变化，或日志提供等价直接证据。

- [ ] 启动 server helper，等待输出 `ConnectLobbyArg`，保留其进程与日志。
- [ ] 用 `D:\Steam\steam.exe -applaunch 550 -console -novid +connect_lobby <lobbyId>` 启动 L4D2。
- [ ] 轮询 L4D2 console/log，识别成功、reservation mismatch、lobby join failure 或 server rejection。
- [ ] 成功时记录 reservation/connect 值及客户端/服务器证据；失败时只根据日志进入下一轮根因分析。
- [ ] 更新中文 README，说明运行、字段含义、已验证边界、故障诊断和清理方法。

### Task 4: Lobby 房主转让与旧大厅重入

**Files:**
- Create: `research/SteamLobbyProbe/Test-LobbyOwnershipTransfer.ps1`
- Modify: `research/SteamLobbyProbe/Program.cs`
- Modify: `research/SteamLobbyProbe/README.md`
- Modify: `docs/aegis/work/2026-08-14-l4d2-server-lobby/50-evidence.md`

**Why this task exists:** 验证 Steam lobby 所有权转给真实 L4D2 客户端后，`server:connectstring` 和 `server:reservationid` 是否保留，以及原 helper 退出后新房主能否维持同一个大厅供本机客户端重新加入。

**Impact / Compatibility:** 新增 `<steam_api.dll> transfer-owner <lobbyId> <newOwnerSteamId>` 诊断模式；旧 probe、server 和 protocol-reply 模式保持不变。转让模式不创建、加入或离开 lobby，不把目标 lobby 写入 `activeLobbyId`，避免 finally 清理掉被转让的大厅。

**Verification:** 对当前 lobby `109775242109682143` 先运行脚本确认 RED；实现后运行同一脚本完成一次真实转让并确认字段读回不变。随后结束 helper，启动本机 L4D2，通过 `steam://joinlobby/550/109775242109682143/76561199382197988` 加入，使用本机 console.log 和远端 owner 状态判定结果。

- [ ] 新增 `Test-LobbyOwnershipTransfer.ps1`，发布到独立的 `publish-transfer-test`，避免覆盖正在运行的 helper。
- [ ] 脚本调用 `transfer-owner` 并断言转让前 owner、转让结果、转让后 owner、两个字段前后值和新 JoinURI。
- [ ] 在现有实现上运行脚本，确认因缺少 `transfer-owner` 模式而 RED，且 lobby owner 尚未变化。
- [ ] 增加 `RequestLobbyData`、`GetLobbyOwner`、`SetLobbyOwner` flat API delegate 和 wrapper。
- [ ] 实现转让模式：请求 metadata、读回转让前 owner/字段、确认当前 Steam 用户是 owner、调用 SetLobbyOwner、pump callbacks 直到 owner 变化、再次请求并读回字段。
- [ ] 若 owner 未变或字段变化，返回非零并保留完整诊断输出；成功时输出 `NewOwnerJoinURI`。
- [ ] 运行脚本确认 GREEN，并记录新的 owner 与字段值。
- [ ] 结束原 helper，先启动本机 L4D2，再执行新房主 JoinURI。
- [ ] 记录本机客户端是否收到 ReplyJoinData、是否连接目标服务器，以及大厅是否仍可用。
- [ ] 更新中文 README 和证据记录。

## Repair Track

- 根因：外部 probe 只创建普通 lobby，没有 server metadata、Steam game-server binding 和持续生命周期。
- 权威所有者：`SteamLobbyProbe/Program.cs` 负责 Steam lobby；L4D2 负责完整 MatchFramework 帧与实际网络握手。
- 最小变更：新增 server 模式和两个 Steam API wrapper，不把 engine 逻辑复制进 helper。

## Retirement Track

- NativeMatchFrameworkProbe 的 `RunFrame skipped` 分支继续作为研究边界，不进入生产连接路径。
- 旧普通 lobby probe 保留用于回归；当 server 模式稳定后仍不删除，因为它验证 Steam API 基础能力且无重复权威行为。
- 若 L4D2 能靠标准 `+connect_lobby` 加入，则不实现手写 reservation packet。
- `transfer-owner` 仅用于诊断和所有权交接，不替代 server 模式的 lobby 生命周期所有权；若真实 L4D2 新房主不能接管加入协议，该模式保留为证据工具，不作为连接修复路径。

### Task 5: 不转让 owner 的大厅成员关系实验

**Files:**
- Create: `research/SteamLobbyProbe/Test-LobbyMembershipActions.ps1`
- Modify: `research/SteamLobbyProbe/Program.cs`
- Modify: `research/SteamLobbyProbe/README.md`
- Modify: `docs/aegis/work/2026-08-14-l4d2-server-lobby/50-evidence.md`

**Why this task exists:** 区分“Steam 账号仍属于原大厅”与“L4D2 网络连接仍存在”两种状态，确认能否让远端退出或切换大厅而不退出 24561。

**Impact / Compatibility:** 新增 `create-lobby-hold` 和 `leave-lobby` 诊断命令；不修改旧命令，不转让 owner，不向游戏发送控制台连接/断开命令。

**Verification:** 先运行新脚本确认命令缺失导致 RED；实现后确认 GREEN。端到端分别从干净基线执行两个动作，并联合核验 A2S 玩家数、远端 PID、客户端日志、原大厅 callback 和新大厅 ID。

- [ ] 新增契约测试，启动短时 `create-lobby-hold`，解析新大厅 ID，再用第二进程对该大厅调用 `leave-lobby`。
- [ ] 运行测试并确认因现有 CLI 不识别新命令而 RED。
- [ ] 增加 `GetNumLobbyMembers` 与 `GetLobbyMemberByIndex` flat API wrapper。
- [ ] 实现 `create-lobby-hold [type] [seconds]`，仅泵回调并在 finally 离开新大厅。
- [ ] 实现 `leave-lobby <lobbyId> [settleSeconds]`，记录调用前后当前账号的成员状态。
- [ ] 实现 `join-lobby-hold <lobbyId> [seconds]`，作为远端 CreateLobby 被限流时的第二大厅实验路径。
- [ ] 运行契约测试、现有三个 PowerShell 测试和 Release build。
- [ ] 发布远端诊断程序，通过交互式计划任务运行。
- [ ] 从头执行“创建第二大厅”实验并记录联合证据。
- [ ] 从头执行“离开原大厅”实验并记录联合证据。
- [ ] 更新中文 README 和证据文档。

#### Repair Track

- 根因待验证：此前 owner 转让后远端退出服务器，无法区分是 owner/lobby 生命周期变化还是客户端主动离开当前 MatchFramework session。
- 权威所有者：Steam lobby membership 由 `ISteamMatchmaking` 管理；游戏网络连接由 L4D2 管理。
- 最小变更：只增加可观测的 lobby membership 操作，不增加连接控制逻辑。

#### Retirement Track

- owner transfer 不再参与本实验路径，保留仅用于复现历史现象。
- 新诊断命令若证明不会保留服务器连接，只作为负面证据工具，不升级为产品工作流。

### Task 6: 真实 reservation lobby 只读枚举

**Files:**
- Create: `research/SteamLobbyProbe/Inspect-LobbyDiagnostic.ps1`
- Modify: `research/SteamLobbyProbe/README.md`
- Modify: `docs/aegis/work/2026-08-14-l4d2-server-lobby/50-evidence.md`

**Why this task exists:** 真实 L4D2 客户端已使服务器显示 `reserved 186000047bb2ba4`，需要确定该 cookie 是否能从普通 Steam lobby metadata 或 game-server binding 中读回。

**Impact / Compatibility:** 新脚本仅调用 `RequestLobbyData` 和 Get 系列 API；不创建、加入、离开或修改 lobby，不影响正在运行的 L4D2 与服务器 reservation。

**Verification:** 对十进制 lobby ID `109775242120604580` 运行脚本，输出 owner、全部成员、全部 metadata 和 `GetLobbyGameServer`；将结果与服务器 status 中的 reservation cookie 交叉核对。

- [x] 从 64 位入口自动转入 32 位 PowerShell，并用 AppID 550 初始化 Steam API。
- [x] 请求 lobby 数据并泵回调至少 2 秒。
- [x] 枚举 owner、成员、metadata 和 game-server binding，确认执行路径没有 Set/Join/Leave API。
- [x] 记录完整输出并说明 cookie 的实际状态归属。
