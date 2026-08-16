# App Usage Event Experiment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `aegis:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Test whether sending Steam's legacy `ClientAppUsageEvent(GameLaunch, AppID 550, Offline=0)` lets a real L4D2 client join an ASF-owned lobby without a graphical Steam client.

**Architecture:** A small custom SteamKit2 handler owns the raw, legacy CM message because only a registered `ClientMsgHandler` has access to ASF's connected `SteamClient`. An opt-in plugin configuration flag emits the event, then reuses the previously tested `PLAY 550` call before normal lobby creation. All lobby metadata, reservation settings, JoinData handling, endpoint selection, and default behavior stay unchanged.

**Tech Stack:** .NET 10, ASF 6.3.8.4 plugin API, SteamKit2 3.4.0, MSTest, Docker, remote Edge L4D2 client.

**Baseline / Authority Refs:** `20-spec.md`, `50-evidence.md`, `L4d2LobbyProbePlugin.cs`, `L4d2LobbyChatHandler.cs`; SteamKit2 3.4.0 `MsgClientAppUsageEvent` serializes a 14-byte legacy body comprising `EAppUsageEvent`, `GameID`, and `Offline`.

**Compatibility Boundary:** `PublishGameLaunchBeforeCreate` defaults to `false`. When disabled, no new CM message and no `PLAY` call occur. The live test changes only this opt-in flag; the remote pre-test DLL and configuration are restored afterward.

**Verification:** A RED/GREEN unit test validates the exact outgoing body. Plugin unit tests and Release build must pass. Live evidence requires the plugin log to record the GameLaunch event before lobby creation, then an Edge L4D2 URI attempt within the reservation window; membership and `join_data` logs distinguish client lobby entry from later reservation behavior.

---

### Task 1: Encode and Send the App Usage Event

**Files:**
- Create: `research/L4d2AsfPlugin/L4d2AppUsageEvent.cs`
- Create: `research/L4d2AsfPlugin/L4d2AppUsageHandler.cs`
- Create: `research/L4d2AsfPlugin/tests/L4d2AppUsageEventTests.cs`
- Modify: `research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj`

**Why this task exists:** It creates one auditable owner for the exact legacy CM message, preventing the plugin lifecycle from hand-building protocol bytes.

**Repair Track:** The candidate source difference is the absence of `ClientAppUsageEvent` in ASF's normal `PLAY` path. The new handler is the canonical owner for this optional message.

**Retirement Track:** No existing sender is replaced. The handler remains inactive unless Task 2's flag is enabled; remove it if the live test has no positive evidence.

**Verification:** `dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo`

- [ ] **Step 1: Write the failing test**

```csharp
[TestMethod]
public void GameLaunchMessageUsesL4d2OnlineGameId()
{
    var message = L4d2AppUsageEvent.CreateGameLaunch();

    Assert.AreEqual(EAppUsageEvent.GameLaunch, message.Body.AppUsageEvent);
    Assert.AreEqual(550U, message.Body.GameID.AppID);
    Assert.AreEqual((ushort) 0, message.Body.Offline);
}
```

- [ ] **Step 2: Run the test to verify RED**

Run: `dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo`

Expected: compilation fails because `L4d2AppUsageEvent` does not exist.

- [ ] **Step 3: Write the minimal implementation**

```csharp
internal static class L4d2AppUsageEvent
{
    internal const uint AppId = 550;

    internal static ClientMsg<MsgClientAppUsageEvent> CreateGameLaunch() => new()
    {
        Body =
        {
            AppUsageEvent = EAppUsageEvent.GameLaunch,
            GameID = new GameID(AppId),
            Offline = 0
        }
    };
}
```

`L4d2AppUsageHandler.PublishGameLaunch()` calls `Client.Send(L4d2AppUsageEvent.CreateGameLaunch())` only when SteamKit has initialized its `Client` property. It handles no inbound messages.

- [ ] **Step 4: Run the test to verify GREEN**

Run: `dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo`

Expected: all tests pass, including `GameLaunchMessageUsesL4d2OnlineGameId`.

### Task 2: Gate the Existing Lifecycle Behind an Opt-In Experiment Flag

**Files:**
- Modify: `research/L4d2AsfPlugin/L4d2PluginConfiguration.cs`
- Modify: `research/L4d2AsfPlugin/L4d2LobbyProbePlugin.cs`
- Modify: `research/L4d2AsfPlugin/tests/L4d2PluginConfigurationTests.cs`

**Why this task exists:** It makes the experimental traffic explicit, reversible, and isolated from normal ASF lobby behavior.

**Impact / Compatibility:** `PublishGameLaunchBeforeCreate=false` retains the current lifecycle byte-for-byte. With the flag true, order is `ClientAppUsageEvent` -> `PLAY 550` -> 2-second propagation delay -> `CreateLobby`.

**Verification:** Target tests, full plugin test suite, and Release build against ASF 6.3.8.4.

- [ ] **Step 1: Write the failing configuration test**

```csharp
[TestMethod]
public void AppUsageExperimentFlagDefaultsOffAndCanBeEnabled()
{
    Assert.IsFalse(GetPublishGameLaunchBeforeCreate(Parse("""{ "EnabledBots": ["sirp"], "Endpoint": "106.54.197.3:24561" }""")));
    Assert.IsTrue(GetPublishGameLaunchBeforeCreate(Parse("""{ "EnabledBots": ["sirp"], "Endpoint": "106.54.197.3:24561", "PublishGameLaunchBeforeCreate": true }""")));
}
```

- [ ] **Step 2: Run the target tests to verify RED**

Run: `dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo`

Expected: the reflection helper cannot find `PublishGameLaunchBeforeCreate`.

- [ ] **Step 3: Implement the opt-in lifecycle**

Add the boolean to `L4d2PluginConfiguration` and its JSON document. Register one `L4d2AppUsageHandler` per enabled bot in `OnBotSteamHandlersInit`. In `OnBotLoggedOn`, when the flag is true, send GameLaunch, require `bot.Actions.Play([550])` success, wait two seconds, then execute the existing `CreateLobby` flow. Log only event names and Steam IDs/lobby IDs; never log credentials.

- [ ] **Step 4: Run local regression and build**

Run:

```powershell
dotnet test research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj --nologo
dotnet build research/L4d2AsfPlugin/L4d2AsfPlugin.csproj -c Release --nologo -p:ASFSourcePath='C:\Users\Administrator.DESKTOP-465SP1L\AppData\Local\Temp\asf-src-6.3.8.4'
```

Expected: zero failures and a Release DLL.

### Task 3: Run and Roll Back the Live Experiment

**Files:**
- Modify temporarily on remote ASF host: plugin DLL and `L4d2AsfPlugin.json`
- Modify temporarily on Edge: an isolated interactive scheduled task
- Modify: `docs/aegis/work/2026-08-15-l4d2-asf-reservation/50-evidence.md`

**Why this task exists:** The actual acceptance boundary is whether a real L4D2 client enters the ASF-owned lobby and sends `RequestJoinData`, which unit tests cannot emulate.

**Impact / Compatibility:** Back up the exact remote DLL/config and restore both after evidence collection. Do not change server settings, RCON, metadata, reservation encoding, or Edge's permanent tasks.

**Verification:** Capture plugin ordering, lobby URI, Edge's valid lobby-members snapshot after its client launches, and `join_data` logs. A lack of membership or JoinData is a negative result, not a successful reservation test.

- [ ] **Step 1: Back up and deploy**

Back up the remote plugin DLL and JSON without printing configuration contents. Build a temporary JSON with only `PublishGameLaunchBeforeCreate=true`, deploy it with the Release DLL, then restart only the ASF container.

- [ ] **Step 2: Run the Edge client inside the reservation window**

Read the newly logged URI, create an isolated one-shot InteractiveToken scheduled task on Edge that invokes `RestartEdgeL4d2Lobby.py <new-lobby-id>`, and run it. Create a separate read-only inspection task after launch to capture lobby members.

- [ ] **Step 3: Collect results and restore**

Collect ASF log lines for `GameLaunch`, `PLAY`, lobby creation, reservation, and `join_data`; collect Edge membership output. Restore the previous DLL/config and restart ASF. Delete only the newly created temporary Edge tasks.

- [ ] **Step 4: Record outcome**

Append the observed ordering and the exact membership/JoinData result to `50-evidence.md`, explicitly separating a protocol-send result from a successful L4D2 join.
