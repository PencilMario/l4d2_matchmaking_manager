# ASF L4D2 Reservation 和 JoinData 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** 让 ASF bot 创建真实 L4D2 lobby，处理二进制 JoinData，并在配置启用时对目标服务器执行 reservation。

**Architecture:** 将纯协议代码收敛到 L4d2Protocol 多目标库；ASF 插件只拥有配置、bot 会话和 SteamKit2 raw chat transport。自定义 handler 读写 ClientMsg<MsgClientChatMsg> 的精确 payload，避免字符串 API 追加 NUL。

**Tech Stack:** C#、.NET 8/10、MSTest、SteamKit2 3.4.0、ASF 6.3.8.4、UDP。

**Baseline / Authority Refs:** 20-spec.md、10-baseline-readset.md、research/SteamLobbyProbe 协议模块、ASF IBotSteamClient。

**Compatibility Boundary:** SteamLobbyProbe 的 CLI 和 PowerShell 固定向量继续可用；无有效 ASF 插件配置时不得创建 lobby；不改变 ASF 本体、不用私有反射、不记录 RCON 凭据。

**Verification:** 协议库和插件 MSTest、SteamLobbyProbe PowerShell 回归、Release build、远端 ASF 日志、24561 客户端连接、27083 RCON/status 和客户端入服。

---

### Task 1: 提取共享协议库

**Files:**
- Create: research/L4d2Protocol/L4d2Protocol.csproj
- Create: research/L4d2Protocol/Properties/AssemblyInfo.cs
- Move: research/SteamLobbyProbe/{BinaryKeyValues,IceCipher,LobbyJoinProtocol,RealSessionSettings,ReservationProtocol}.cs
- Create: research/L4d2Protocol/ReservationClient.cs
- Create: research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj
- Create: research/L4d2Protocol/tests/ReservationClientTests.cs
- Modify: research/SteamLobbyProbe/SteamLobbyProbe.csproj
- Modify: research/SteamLobbyProbe/ReservationCommand.cs

**Why this task exists:** ASF 和 helper 必须使用同一份 settings、ICE 和 JoinData 编码，避免两个实现漂移。

**Repair Track:** canonical protocol owner 迁移到 L4d2Protocol；helper 的命令层保留，但不再拥有协议类型。

**Retirement Track:** helper 中的五个协议源文件移除；旧 ReservationCommand.RunLive() 改为只格式化输出并调用共享 client。若固定向量回归失败，迁移不生效。

**Verification:** dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo；随后执行 Test-ReservationProtocol.ps1、Test-LobbyJoinProtocol.ps1、Test-RealLobbyJoinProtocol.ps1。

- [x] **Step 1: 写 reservation client 的失败测试**

在 ReservationClientTests.cs 写 UDP fixture：先回 CreateChallengeRequest() 对应的 challenge，再捕获 reservation request，并返回严格 10 字节 accepted reply。

~~~csharp
[TestMethod]
public async Task ReserveAsyncSendsTypedSettingsAndReportsAcceptance() {
    using var server = new ReservationFixture();
    var result = await ReservationClient.ReserveAsync(
        server.Endpoint, 0x0186000047CF0FD8, RealSessionSettings.EncodeReservationSettings(),
        TimeSpan.FromSeconds(1), ReservationProtocol.DefaultHostVersion);

    Assert.AreEqual(ReservationOutcome.Accepted, result.Outcome);
    CollectionAssert.AreEqual(RealSessionSettings.EncodeReservationSettings(), server.ReceivedSettings);
}
~~~

- [x] **Step 2: 运行 RED**

Run: dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo

Expected: 编译失败，因 L4d2Protocol、ReservationClient、ReservationFixture 和 ReservationOutcome 尚不存在。

- [x] **Step 3: 建立共享项目并移动协议源**

创建多目标项目，并以 friend assembly 维持当前 internal API：

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
~~~

AssemblyInfo.cs：

~~~csharp
[assembly: InternalsVisibleTo("SteamLobbyProbe")]
[assembly: InternalsVisibleTo("L4d2AsfPlugin")]
[assembly: InternalsVisibleTo("L4d2Protocol.Tests")]
~~~

更新 SteamLobbyProbe.csproj：

~~~xml
<ItemGroup>
  <ProjectReference Include="../L4d2Protocol/L4d2Protocol.csproj" />
</ItemGroup>
~~~

- [x] **Step 4: 实现异步 UDP client**

ReservationClient.ReserveAsync() 创建 IPv4 UDP socket，发送 challenge、严格解析 challenge、创建 ICE packet、发送 packet，并区分 accepted、rejected、host version mismatch 和 timeout。

~~~csharp
internal enum ReservationOutcome { Accepted, Rejected, HostVersionMismatch, Timeout }
internal readonly record struct ReservationResult(ReservationOutcome Outcome, ReservationPacket Packet);
~~~

timeout 返回 Timeout，不得伪称 Accepted；result 仍带回实际发送的 packet。

- [x] **Step 5: 运行 GREEN 与 helper 回归**

Run:

~~~powershell
dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo
Push-Location research/SteamLobbyProbe
./Test-ReservationProtocol.ps1
./Test-LobbyJoinProtocol.ps1
./Test-RealLobbyJoinProtocol.ps1
Pop-Location
~~~

Expected: 所有测试通过，旧 CLI 固定 ICE vector 和 ReplyJoinData vector 不变。

### Task 2: 配置和会话状态

**Files:**
- Create: research/L4d2AsfPlugin/L4d2PluginConfiguration.cs
- Create: research/L4d2AsfPlugin/L4d2HostSession.cs
- Create: research/L4d2AsfPlugin/tests/L4d2PluginConfigurationTests.cs
- Create: research/L4d2AsfPlugin/tests/L4d2HostSessionTests.cs
- Modify: research/L4d2AsfPlugin/L4d2AsfPlugin.csproj
- Modify: research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj

**Why this task exists:** 默认禁用和每 bot 单会话是避免 ASF 重启或配置错误创建意外 lobby 的安全边界。

**Impact / Compatibility:** 当前 probe 的无配置行为从创建后等待 20 秒替换为不创建；实际配置必须显式白名单 bot 和 endpoint。

**Verification:** dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo。

- [ ] **Step 1: 写配置 RED 测试**

覆盖无配置禁用、大小写不敏感 bot 白名单、IPv4 ip:port、成员上限 1..8：

~~~csharp
[TestMethod]
public void MissingConfigurationDisablesAllBots() {
    var result = L4d2PluginConfiguration.TryParse("{}", out var configuration, out _);
    Assert.IsTrue(result);
    Assert.IsFalse(configuration.IsEnabledFor("sirp"));
}
~~~

- [ ] **Step 2: 运行 RED**

Run: dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo

Expected: 编译失败，因 L4d2PluginConfiguration 不存在。

- [ ] **Step 3: 实现严格配置模型**

从 DLL 同目录 L4d2AsfPlugin.json 加载：

~~~csharp
internal sealed record L4d2PluginConfiguration(
    IReadOnlySet<string> EnabledBots, IPEndPoint Endpoint, bool ReservationEnabled,
    ELobbyType LobbyType, int MaxMembers, TimeSpan ReservationTimeout, int HostVersion) {
    internal bool IsEnabledFor(string botName) => EnabledBots.Contains(botName);
    internal static bool TryLoad(string directory, out L4d2PluginConfiguration configuration, out string error);
}
~~~

缺失文件返回禁用配置；存在但无效的 JSON、endpoint 或成员数返回 false，错误不含凭据。

- [ ] **Step 4: 写会话 RED 测试**

对 L4d2HostSession.TryCreateReply() 写 request fixture：session lobby ID、owner ID、endpoint 匹配时生成 real reply；其他 lobby、身份不一致或 Failed 状态均返回 false。

- [ ] **Step 5: 实现会话状态模型**

~~~csharp
internal enum L4d2HostSessionState {
    Creating, Ready, ReservationPending, ReservationResponseReceived,
    ReservationStatusRequired, Failed
}
internal sealed record L4d2HostSession(
    SteamID LobbyId, ulong OwnerSteamId, IPEndPoint Endpoint, L4d2HostSessionState State);
~~~

TryCreateReply() 必须调用 LobbyJoinProtocol.TryCreateRealReply()，只能在 Ready、ReservationResponseReceived 或 ReservationStatusRequired 返回 reply。

- [ ] **Step 6: 运行 GREEN**

Run: dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo

Expected: 新配置和 session 测试通过，原 metadata 测试仍通过。

### Task 3: 原始 SteamKit2 lobby chat transport

**Files:**
- Create: research/L4d2AsfPlugin/L4d2LobbyChatHandler.cs
- Create: research/L4d2AsfPlugin/L4d2LobbyChatMessage.cs
- Create: research/L4d2AsfPlugin/tests/L4d2LobbyChatMessageTests.cs

**Why this task exists:** L4D2 只接受 binary KeyValues payload；ASF 字符串聊天接口会改变消息字节。

**Impact / Compatibility:** handler 只处理 EMsg.ClientChatMsg 且 chat room 等于 active lobby；所有其他 Steam 流量继续由 ASF/SteamKit 正常处理。

**Verification:** dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo。

- [ ] **Step 1: 写 raw payload RED 测试**

用实际 Reply fixture 验证 L4d2LobbyChatMessage.CreateOutbound() 的 payload 精确等于输入，末尾不多一个 0x00：

~~~csharp
[TestMethod]
public void OutboundChatPreservesBinaryPayloadWithoutTerminator() {
    var reply = Convert.FromHexString(ReplyFixture);
    var message = L4d2LobbyChatMessage.CreateOutbound(new SteamID(LobbyId), new SteamID(OwnerId), reply);
    var payload = L4d2LobbyChatMessage.ReadPayload(message);
    CollectionAssert.AreEqual(reply, payload);
}
~~~

- [ ] **Step 2: 运行 RED**

Run: dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo

Expected: 编译失败，因 raw message helper 不存在。

- [ ] **Step 3: 实现 outbound helper 和 handler**

~~~csharp
var outgoing = new ClientMsg<MsgClientChatMsg>(payload.Length);
outgoing.Body.ChatMsgType = EChatEntryType.ChatMsg;
outgoing.Body.SteamIdChatRoom = lobbyId;
outgoing.Body.SteamIdChatter = senderId;
outgoing.Payload.Write(payload, 0, payload.Length);
~~~

handler 的 HandleMsg(IPacketMsg packet) 仅对 EMsg.ClientChatMsg 构造 ClientMsg<MsgClientChatMsg>，读取 Payload.ToArray()，通过 session registry 获取 reply，最后调用受保护的 Client.Send(outgoing)。不得使用 WriteNullTermString() 或 SteamFriends.SendChatRoomMessage()。

- [ ] **Step 4: 运行 GREEN**

Run: dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo

Expected: payload round-trip 逐字节通过；所有旧插件测试通过。

### Task 4: 用配置驱动的 ASF host session 替换 probe

**Files:**
- Modify: research/L4d2AsfPlugin/L4d2LobbyProbePlugin.cs
- Modify: research/L4d2AsfPlugin/LobbyMetadata.cs
- Modify: research/L4d2AsfPlugin/tests/LobbyMetadataTests.cs
- Modify: research/L4d2AsfPlugin/README.md

**Why this task exists:** 把已验证的 metadata、reservation 和 Reply transport 组织为一个真正可部署的 ASF plugin。

**Impact / Compatibility:** plugin 仍只在 configured bot 登录后创建一次 session；无配置时完全无副作用。

**Verification:** 本地插件单测、Release build、配置缺失的远端 ASF 启动日志。

- [ ] **Step 1: 写 metadata/profile RED 测试**

~~~csharp
Assert.AreEqual("versus", metadata["Game:Mode"]);
Assert.AreEqual("game", metadata["Game:state"]);
Assert.IsFalse(metadata.Keys.Any(key => key.StartsWith("Server:", StringComparison.Ordinal)));
~~~

- [ ] **Step 2: 运行 RED**

Run: dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo

Expected: 失败，因为当前 probe metadata 不是完整 profile。

- [ ] **Step 3: 实现 bot lifecycle**

plugin 同时实现 IBotConnection、IBotSteamClient。OnBotSteamHandlersInit() 为每个 bot 返回一个 handler；OnBotLoggedOn() 仅在有效配置和白名单匹配时创建 Public lobby、写 RealSessionSettings.CreateLobbyMetadata()、将 L4d2HostSession 发布给 registry。

~~~csharp
var result = await ReservationClient.ReserveAsync(
    config.Endpoint, lobbyId.ConvertToUInt64(),
    RealSessionSettings.EncodeReservationSettings(),
    config.ReservationTimeout, config.HostVersion);
~~~

accepted 进入 ReservationResponseReceived；timeout 进入 ReservationStatusRequired 并输出 verification=server_status_required；rejected 或 host mismatch 进入 Failed 并 leave lobby。

- [ ] **Step 4: 运行 GREEN、构建与副作用检查**

Run:

~~~powershell
dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo
dotnet build research/L4d2AsfPlugin/L4d2AsfPlugin.csproj -c Release --nologo -p:ASFSourcePath='C:\Users\Administrator.DESKTOP-465SP1L\AppData\Local\Temp\asf-src-6.3.8.4'
~~~

Expected: 所有测试通过，Release 为 0 warning/0 error。更新 README 中 JSON 配置、日志状态、部署与清理流程；不得写 RCON 密码。

### Task 5: Docker staging 和客户端 transport 验收

**Files:**
- Modify: research/L4d2AsfPlugin/README.md
- Create: docs/aegis/work/2026-08-15-l4d2-asf-reservation/50-evidence.md

**Why this task exists:** 24561 用于把 SteamKit raw transport 和 reservation 服务器行为分开取证。

**Verification:** 远端 ASF health、plugin 日志、真实客户端控制台、24561 server status。

- [ ] **Step 1: 部署非 reservation 配置**

在 100.72.137.92 的 /home/sirp/archisteamfarm/plugins/L4d2AsfPlugin/ 放入 Release DLL、deps 和以下配置，重启 archisteamfarm：

~~~json
{"EnabledBots":["sirp"],"Endpoint":"106.54.197.3:24561","ReservationEnabled":false,"LobbyType":"Public","MaxMembers":8}
~~~

- [ ] **Step 2: 取证并让真实客户端加入**

从 ASF 日志记录 lobby ID 和 JoinURI。客户端通过 URI 加入；日志必须同时包含 RequestJoinData sender、ReplyJoinData byte count 和非空 session state。客户端显示连接 106.54.197.3:24561；服务器 status 显示客户端 active。

- [ ] **Step 3: 清理或准备下一阶段**

transport 成功后仅替换 JSON endpoint/reservation flag；失败时删除插件目录并重启 ASF，保留日志和 lobby ID 作为证据。不得通过 server:* metadata 绕过 raw reply 问题。

### Task 6: 27083 reservation 和真实客户端入服验收

**Files:**
- Modify: docs/aegis/work/2026-08-15-l4d2-asf-reservation/50-evidence.md

**Why this task exists:** 完整目标要求服务器接受 ASF 创建的 cookie 且真实客户端经 lobby 入服。

**Verification:** 27083 RCON/status 中的 reserved cookie、ASF reservation/JoinData 日志、客户端 console 和 server 玩家状态。

- [ ] **Step 1: 部署 reservation 配置**

~~~json
{"EnabledBots":["sirp"],"Endpoint":"202.105.108.88:27083","ReservationEnabled":true,"LobbyType":"Public","MaxMembers":8,"ReservationTimeoutMilliseconds":5000,"HostVersion":2243}
~~~

重启 ASF，记录新 lobby CSteamID。若 reservation response timeout，保留 ReservationStatusRequired 状态，先用授权 RCON/status 核对 cookie；不得把 timeout 当作成功或拒绝。

- [ ] **Step 2: 验证 reservation**

在客户端加入前取得服务器 status，确认出现：

~~~text
(reserved <lobby-id 的小写十六进制>)
~~~

该值与 ASF 日志 lobby CSteamID 相同才可进入下一步。

- [ ] **Step 3: 验证真实客户端入服**

客户端用 steam://joinlobby/550/<lobby-id>/<owner-id> 加入；ASF 日志显示 raw Request/Reply，客户端 console 显示连接 202.105.108.88:27083，服务器 status 显示该客户端为 active human。

- [ ] **Step 4: 清理与记录**

删除远端 plugin 目录并重启 ASF，或将 JSON 改为无 enabled bots；确认容器 healthy running。在 50-evidence.md 记录命令、cookie、日志摘要、RCON/status 和客户端结果，省略凭据。

## 已知未知项与回滚

- 若 100.72.137.92 无法收到 27083 challenge，先保留本地 protocol green 证据与 24561 transport 结果；这说明网络出口需调整，不代表 JoinData 编码错误。
- 远端 staging 随时可通过删除独立 plugin 目录并重启 ASF 回滚；ASF 本体、bot config 和游戏服务器均不会被修改。
- 项目非 Git 工作树；每个任务以测试输出和 50-evidence.md 代替提交记录。
