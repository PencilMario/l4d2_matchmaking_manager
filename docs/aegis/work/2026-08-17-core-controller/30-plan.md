# L4D2 Single-Host Core Controller Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Deliver a Docker-deployed single-host controller that schedules L4D2 lobby warm-up through persistent, per-account Steam Agent sessions.

**Architecture:** L4d2MatchmakingCore is an authenticated ASP.NET Core control plane. PostgreSQL stores configuration, operation correlations, audit and leases, while Core makes scheduling decisions from fresh Agent and A2S observations. Each Agent owns one persistent Steam Manual Dispatch actor, which creates randomly-selected C1-C14 profiles, holds lobbies and reads arbitrary lobby data.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, MSTest, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Docker.DotNet 3.125.15, Testcontainers.PostgreSql 4.14.0, PostgreSQL 17, Docker Compose.

**Baseline / Authority Refs:** docs/aegis/work/2026-08-17-core-controller/20-spec.md, 10-baseline-readset.md, CONTEXT.md, docs/steam-lobby-agent-api.md, docs/steam-lobby-automation.md, research/SteamLobbyProbe/Program.cs, research/L4d2Protocol/RealSessionSettings.cs.

**Compatibility Boundary:** Agent GET /healthz and GET /v1/probe/status retain their routes, payload fields and stable failures. Agent control APIs are Docker-internal only. The former Core docker-exec approach is not a business path. Persisted data is never a substitute for live Steam, Agent or A2S state.

**Verification:** Run dotnet test L4d2MatchmakingManager.sln, pwsh deploy/steam-lobby-agent/Test-ComposeContract.ps1, pwsh deploy/matchmaking-core/Test-ComposeContract.ps1, docker compose -f deploy/matchmaking-core/docker-compose.yml config, then complete the Task 12 Ubuntu acceptance journey.

---

### Task 1: Establish shared contracts and Core projects

**Files:**
- Create: L4d2MatchmakingManager.sln
- Create: src/L4d2Matchmaking.Contracts/L4d2Matchmaking.Contracts.csproj
- Create: src/L4d2Matchmaking.Contracts/AgentContracts.cs
- Create: src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj
- Create: src/L4d2Matchmaking.Contracts/tests/AgentContractsSerializationTests.cs
- Create: src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj
- Create: src/L4d2MatchmakingCore/Program.cs
- Create: src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj

**Why this task exists:** Core and Agent need one type-safe internal protocol for operations and lobby snapshots. Steam and lobby IDs must serialize as strings.

**Impact / Compatibility:** New projects only. Existing Agent routes and Probe CLI do not change.

**Verification:** dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj; dotnet build L4d2MatchmakingManager.sln -warnaserror.

- [x] **Step 1: Write the failing contracts round-trip test**

~~~csharp
[TestMethod]
public void LobbySnapshotSerializesSteamIdentifiersAsStrings()
{
    var snapshot = new LobbySnapshot(
        "109775242170052468",
        "76561198000000000",
        [new LobbyMemberSnapshot("76561198000000000", "Agent")],
        new Dictionary<string, string> { ["Game:campaign"] = "L4D2C2" },
        DateTimeOffset.UnixEpoch);
    var json = JsonSerializer.Serialize(snapshot);
    StringAssert.Contains(json, "\"109775242170052468\"");
    var restored = JsonSerializer.Deserialize<LobbySnapshot>(json);
    Assert.IsNotNull(restored);
    Assert.AreEqual(snapshot.LobbyId, restored.LobbyId);
    Assert.AreEqual(snapshot.OwnerSteamId, restored.OwnerSteamId);
    CollectionAssert.AreEqual(snapshot.Members.ToArray(), restored.Members.ToArray());
    CollectionAssert.AreEquivalent(snapshot.Metadata.ToArray(), restored.Metadata.ToArray());
    Assert.AreEqual(snapshot.ObservedAt, restored.ObservedAt);
}
~~~

- [x] **Step 2: Run the test and confirm RED**

Run: dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj

Expected: compile failure because LobbySnapshot does not exist.

- [x] **Step 3: Create the solution, projects and minimal contracts**

~~~csharp
public enum AgentLobbyMode { Standard, Reserved }

public sealed record AgentOperationRequest(Guid OperationId, AgentLobbyMode Mode, string Ipv4Address, ushort Port);
public sealed record LobbyMemberSnapshot(string SteamId, string? PersonaName);
public sealed record LobbySnapshot(string LobbyId, string OwnerSteamId, IReadOnlyList<LobbyMemberSnapshot> Members, IReadOnlyDictionary<string, string> Metadata, DateTimeOffset ObservedAt);
public sealed record AgentOperationSnapshot(Guid OperationId, string State, LobbySnapshot? Lobby, string? Failure, DateTimeOffset ObservedAt);
public sealed record AgentOperationStartResult(AgentOperationSnapshot Operation, bool AlreadyExists);
public sealed record AgentHealthSnapshot(bool Ready, string? Failure, DateTimeOffset ObservedAt);
~~~

Create the solution with dotnet new sln -n L4d2MatchmakingManager. Add the new Core and Contracts projects and their test projects; retain the existing Agent project and tests. Reference Contracts from Core, Agent and SteamLobbyProbe.

- [x] **Step 4: Run the contracts and build checks**

Run: dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj
Run: dotnet build L4d2MatchmakingManager.sln -warnaserror

Expected: the JSON test and all project builds pass.

- [x] **Step 5: Commit the boundary**

~~~text
git add L4d2MatchmakingManager.sln src/L4d2Matchmaking.Contracts src/L4d2MatchmakingCore
git commit -m "feat(contracts): 增加核心与 Agent 内部协议"
~~~

### Task 2: Parameterize the complete C1-C14 campaign profile

**Files:**
- Create: research/L4d2Protocol/CampaignProfile.cs
- Modify: research/L4d2Protocol/RealSessionSettings.cs
- Create: research/L4d2Protocol/tests/CampaignProfileTests.cs
- Modify: research/SteamLobbyProbe/Program.cs
- Modify: research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj

**Why this task exists:** Every new lobby must use one whole official campaign profile. C14's author and SurvivorSet must never be combined with another campaign.

**Impact / Compatibility:** Parameterless RealSessionSettings methods keep C2 as their historical default for existing vectors. The 28 metadata keys and all non-map values remain unchanged.

**Verification:** dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj; dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj.

- [x] **Step 1: Write the failing C14 and catalog tests**

~~~csharp
[TestMethod]
public void OfficialProfilesKeepC14FieldsTogether()
{
    Assert.AreEqual(14, CampaignProfile.Official.Count);
    var c14 = CampaignProfile.Official.Single(x => x.CampaignId == "L4D2C14");
    Assert.AreEqual("Valve, NF, Roku, Jaiz, Wolphin", c14.Author);
    Assert.AreEqual(1, c14.SurvivorSet);
    Assert.AreEqual("missions/campaign14.txt", c14.MissionFile);
}
~~~

- [x] **Step 2: Run the protocol test and confirm RED**

Run: dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --filter CampaignProfileTests

Expected: compile failure because CampaignProfile is absent.

- [x] **Step 3: Implement immutable profiles and parameterized settings**

~~~csharp
public sealed record CampaignProfile(string CampaignId, string DisplayTitle, string MissionFile, string Author, int SurvivorSet)
{
    public static IReadOnlyList<CampaignProfile> Official { get; } =
    [
        new("L4D2C1", "#L4D360UI_CampaignName_C1", "missions/campaign1.txt", "Valve", 2),
        new("L4D2C2", "#L4D360UI_CampaignName_C2", "missions/campaign2.txt", "Valve", 2),
        new("L4D2C3", "#L4D360UI_CampaignName_C3", "missions/campaign3.txt", "Valve", 2),
        new("L4D2C4", "#L4D360UI_CampaignName_C4", "missions/campaign4.txt", "Valve", 2),
        new("L4D2C5", "#L4D360UI_CampaignName_C5", "missions/campaign5.txt", "Valve", 2),
        new("L4D2C6", "#L4D360UI_CampaignName_C6", "missions/campaign6.txt", "Valve", 2),
        new("L4D2C7", "#L4D360UI_CampaignName_C7", "missions/campaign7.txt", "Valve", 1),
        new("L4D2C8", "#L4D360UI_CampaignName_C8", "missions/campaign8.txt", "Valve", 1),
        new("L4D2C9", "#L4D360UI_CampaignName_C9", "missions/campaign9.txt", "Valve", 1),
        new("L4D2C10", "#L4D360UI_CampaignName_C10", "missions/campaign10.txt", "Valve", 1),
        new("L4D2C11", "#L4D360UI_CampaignName_C11", "missions/campaign11.txt", "Valve", 1),
        new("L4D2C12", "#L4D360UI_CampaignName_C12", "missions/campaign12.txt", "Valve", 1),
        new("L4D2C13", "#L4D360UI_CampaignName_C13", "missions/campaign13.txt", "Valve", 2),
        new("L4D2C14", "#L4D360UI_CampaignName_C14", "missions/campaign14.txt", "Valve, NF, Roku, Jaiz, Wolphin", 1),
    ];
}
~~~

Add CreateReservationSettings(CampaignProfile) and CreateLobbyMetadata(CampaignProfile). Keep parameterless overloads delegating to C2. The Probe and actor select exactly one profile before generating both metadata and reservation settings.

- [x] **Step 4: Run all protocol and Probe regression tests**

Run: dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj
Run: dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj

Expected: new 14-profile tests pass and existing C2 reservation-vector output remains valid.

- [x] **Step 5: Commit metadata ownership**

~~~text
git add research/L4d2Protocol research/SteamLobbyProbe
git commit -m "feat(agent): 支持随机官方战役 metadata 档案"
~~~

### Task 3: Extract one persistent Manual Dispatch Steam session actor

**Files:**
- Create: research/SteamLobbyProbe/SteamSessionContracts.cs
- Create: research/SteamLobbyProbe/SteamSessionActor.cs
- Create: research/SteamLobbyProbe/SteamNativeRuntime.cs
- Modify: research/SteamLobbyProbe/Program.cs
- Modify: research/SteamLobbyProbe/SteamLobbyProbe.csproj
- Create: research/SteamLobbyProbe/tests/SteamSessionActorTests.cs

**Why this task exists:** A long-running keepalive process cannot safely run a second Steam API helper for health or arbitrary lobby reads. One actor must own the account.

**Impact / Compatibility:** Keep the research CLI executable. Move the native SteamApi delegates, callback structs and manual-dispatch code out of Program into a single runtime owner. The Agent stops launching one helper per health request.

**Repair Track:** Move Steam API ownership from short-lived process launchers to SteamSessionActor.

**Retirement Track:** Keep Probe CLI for research diagnostics. Retire Agent use of IProbeProcessLauncher after persistent actor tests and Ubuntu acceptance pass.

**Verification:** dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj.

- [x] **Step 1: Write the failing active-operation query test**

~~~csharp
[TestMethod]
public async Task ReadLobbyUsesTheSameActorWhileAnOperationIsHeld()
{
    var native = new FakeSteamNativeRuntime();
    await using var actor = new SteamSessionActor(native, new FixedCampaignSelector(CampaignProfile.Official[0]));
    var started = await actor.StartAsync(new AgentOperationRequest(Guid.NewGuid(), AgentLobbyMode.Standard, "127.0.0.1", 27015), CancellationToken.None);
    var lobby = await actor.ReadLobbyAsync(109775242170052468UL, CancellationToken.None);
    Assert.IsFalse(started.AlreadyExists);
    Assert.AreEqual(1, native.MaximumConcurrentCalls);
    Assert.AreEqual("109775242170052468", lobby.LobbyId);
}
~~~

- [x] **Step 2: Run the actor test and confirm RED**

Run: dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter SteamSessionActorTests

Expected: compile failure because SteamSessionActor and ISteamNativeRuntime are absent.

- [x] **Step 3: Implement actor contracts, runtime extraction and command loop**

~~~csharp
public interface ISteamSessionActor : IAsyncDisposable
{
    Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken ct);
    Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken ct);
    Task<AgentOperationSnapshot?> GetOperationAsync(Guid operationId, CancellationToken ct);
    Task StopAsync(Guid operationId, CancellationToken ct);
    Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken ct);
}

public interface ICampaignSelector
{
    CampaignProfile Select();
}
~~~

SteamSessionActor owns one single-thread command queue, initializes Manual Dispatch once through SteamNativeRuntime, pumps callbacks at a bounded interval and refreshes the active lobby snapshot. SteamNativeRuntime remains the sole actor-side owner of an initialized Steam session; it reuses the Probe's native export wrapper rather than duplicating ABI delegates. Start writes a randomly-selected CampaignProfile, executes reservation when requested, maps failures to stable codes and holds the lobby until StopAsync calls LeaveLobby.

- [x] **Step 4: Run actor and legacy CLI regression tests**

Run: dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj
Run: dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj

Expected: fake native calls never overlap; existing Probe vector assertions remain green.

- [x] **Step 5: Commit the Steam actor**

~~~text
git add research/SteamLobbyProbe research/L4d2Protocol
git commit -m "refactor(steam): 抽取单账号持久会话 actor"
~~~

### Task 4: Add Agent operation and arbitrary lobby APIs

**Files:**
- Modify: src/L4d2LobbyAgent/Program.cs
- Modify: src/L4d2LobbyAgent/L4d2LobbyAgent.csproj
- Create: src/L4d2LobbyAgent/Steam/AgentSteamSessionService.cs
- Create: src/L4d2LobbyAgent/Steam/AgentSessionOptions.cs
- Modify: src/L4d2LobbyAgent/Probe/ProbeStatusService.cs
- Modify: src/L4d2LobbyAgent/tests/ProbeStatusServiceTests.cs
- Create: src/L4d2LobbyAgent/tests/AgentLobbyEndpointTests.cs

**Why this task exists:** Core needs idempotent start/status/stop operations and Agent-provided arbitrary lobby reads even while that Agent holds another lobby.

**Impact / Compatibility:** Keep health routes, response fields and HTTP 200/503 rules. New routes remain internal Docker endpoints. Reject zero and malformed lobby IDs.

**Verification:** dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj.

- [x] **Step 1: Write failing idempotency and active-state query endpoint tests**

~~~csharp
[TestMethod]
public async Task StartIsIdempotentAndReadLobbyWorksDuringAnActiveOperation()
{
    var id = Guid.NewGuid();
    var request = new AgentOperationRequest(id, AgentLobbyMode.Standard, "203.0.113.7", 27015);
    Assert.AreEqual(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/v1/operations", request)).StatusCode);
    Assert.AreEqual(HttpStatusCode.OK, (await client.PostAsJsonAsync("/v1/operations", request)).StatusCode);
    Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/v1/lobbies/109775242170052468")).StatusCode);
}
~~~

- [x] **Step 2: Run Agent endpoint tests and confirm RED**

Run: dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --filter AgentLobbyEndpointTests

Expected: /v1/operations returns 404.

- [x] **Step 3: Register the singleton actor and map internal routes**

~~~csharp
app.MapPost("/v1/operations", async (AgentOperationRequest request, IAgentSteamSessionService service, CancellationToken ct) =>
{
    var result = await service.StartAsync(request, ct);
    return result.AlreadyExists
        ? Results.Ok(result.Operation)
        : Results.Accepted("/v1/operations/" + request.OperationId, result.Operation);
});
app.MapGet("/v1/operations/{operationId:guid}", async (Guid operationId, IAgentSteamSessionService service, CancellationToken ct) =>
    await service.GetAsync(operationId, ct) is { } snapshot ? Results.Ok(snapshot) : Results.NotFound());
app.MapDelete("/v1/operations/{operationId:guid}", async (Guid operationId, IAgentSteamSessionService service, CancellationToken ct) =>
    await service.StopAsync(operationId, ct) ? Results.NoContent() : Results.NotFound());
app.MapGet("/v1/lobbies/{lobbyId}", async (string lobbyId, IAgentSteamSessionService service, CancellationToken ct) =>
    ulong.TryParse(lobbyId, out var id) && id != 0 ? Results.Ok(await service.ReadLobbyAsync(id, ct)) : Results.BadRequest());
~~~

ProbeStatusService must observe the actor and generate a fresh ObservedAt. Remove Agent registration of the old process launcher after the actor replaces it.

- [x] **Step 4: Run full Agent tests**

Run: dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj

Expected: health compatibility and new 202/200/404/400 endpoint tests pass.

- [x] **Step 5: Commit Agent control APIs**

~~~text
git add src/L4d2LobbyAgent research/SteamLobbyProbe
git commit -m "feat(agent): 增加持久大厅控制和查询接口"
~~~

### Task 5: Make shared-library, download-region and update policy explicit

**Files:**
- Modify: deploy/steam-lobby-agent/91-enable-steam-supervisor.sh
- Modify: deploy/steam-lobby-agent/l4d2-lobby-agent.ini
- Modify: deploy/steam-lobby-agent/docker-compose.yml
- Modify: deploy/steam-lobby-agent/Test-ComposeContract.ps1
- Create: deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1
- Create: deploy/steam-lobby-agent/tests/fixtures/shared-library/steamapps/appmanifest_550.acf
- Modify: deploy/steam-lobby-agent/README.md

**Why this task exists:** L4D2, Steam runtime and Steam API files are shared; only account credentials and user config are isolated. Normal agents must not auto-update AppID 550.

**Impact / Compatibility:** Preserve the shared library mount at /mnt/steam-library and per-Agent steam-data volume. AutoUpdateBehavior belongs to the shared appmanifest_550.acf, not an account volume.

**Verification:** pwsh deploy/steam-lobby-agent/Test-ComposeContract.ps1; pwsh deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1 -SharedLibraryPath deploy/steam-lobby-agent/tests/fixtures/shared-library.

- [x] **Step 1: Write the failing shared-library configuration test**

~~~powershell
# Create deploy/steam-lobby-agent/tests/fixtures/shared-library/steamapps/appmanifest_550.acf:
# "AppState"
# {
#     "appid" "550"
#     "AutoUpdateBehavior" "0"
# }

$manifest = Join-Path $SharedLibraryPath 'steamapps/appmanifest_550.acf'
if (-not (Test-Path -LiteralPath $manifest)) {
    throw "Missing AppID 550 fixture: $manifest"
}

$fixture = Get-Content -Raw -LiteralPath $manifest
if ($fixture -notmatch '"AutoUpdateBehavior"\s+"0"') {
    throw 'The fixture must model Steam default AppID 550 automatic updates.'
}

$initializer = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh')
if ($initializer -notmatch 'set_l4d2_update_policy' -or
    $initializer -notmatch '"AutoUpdateBehavior" "1"' -or
    $initializer -notmatch 'STEAM_DOWNLOAD_REGION') {
    throw 'The initializer must set AppID 550 to update-on-launch and persist the optional region account-locally.'
}

if ($initializer -notmatch '"DisableShaderCache" "1"') {
    throw 'The initializer must disable shader precaching in the account-local Steam configuration.'
}
~~~

- [x] **Step 2: Run it and confirm RED**

Run: pwsh deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1 -SharedLibraryPath deploy/steam-lobby-agent/tests/fixtures/shared-library

Expected: failure because AutoUpdateBehavior 1 is not configured.

- [x] **Step 3: Implement idempotent account and shared manifest configuration**

~~~bash
set_l4d2_update_policy() {
    manifest="$STEAM_SHARED_LIBRARY_PATH/steamapps/appmanifest_550.acf"
    [ -f "$manifest" ] || return 0
    sed -i 's/"AutoUpdateBehavior"[[:space:]]*"[0-9]"/"AutoUpdateBehavior" "1"/' "$manifest"
}
~~~

Write STEAM_DOWNLOAD_REGION only into the account-local Steam VDF. Retain DisableShaderCache 1. Apply the manifest policy only after AppID 550 is installed and document that the Core maintenance lock serializes bootstrap/update before restoring the value to 1.

- [x] **Step 4: Run deployment configuration tests**

Run: pwsh deploy/steam-lobby-agent/Test-ComposeContract.ps1
Run: pwsh deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1 -SharedLibraryPath deploy/steam-lobby-agent/tests/fixtures/shared-library

Expected: shared mount, credential isolation, shader policy and AppID 550 update policy all pass.

- [x] **Step 5: Commit shared-library protection**

~~~text
git add deploy/steam-lobby-agent
git commit -m "feat(deploy): 共享游戏库并禁用常规自动更新"
~~~

### Task 6: Build Core authentication and PostgreSQL persistence

**Files:**
- Modify: src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj
- Modify: src/L4d2MatchmakingCore/Program.cs
- Create: src/L4d2MatchmakingCore/Configuration/CoreOptions.cs
- Create: src/L4d2MatchmakingCore/Auth/StaticBearerAuthenticationHandler.cs
- Create: src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs
- Create: src/L4d2MatchmakingCore/Data/Entities.cs
- Create: src/L4d2MatchmakingCore/Data/Migrations/202608170001_InitialCoreSchema.cs
- Create: src/L4d2MatchmakingCore/tests/CoreAuthenticationTests.cs
- Create: src/L4d2MatchmakingCore/tests/PostgresPersistenceTests.cs

**Why this task exists:** Core needs durable configuration, audit and leases without persisting runtime truth.

**Impact / Compatibility:** Read Bearer token from an environment variable or Docker secret file; never log it. Store lobby IDs as decimal strings. Target-server reservation leases use TargetServerId as their primary key.

**Verification:** dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "CoreAuthenticationTests|PostgresPersistenceTests".

- [ ] **Step 1: Write failing authentication and unique-lease tests**

~~~csharp
[TestMethod]
public async Task ManagementEndpointsRequireBearerToken() =>
    Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/servers")).StatusCode);

[TestMethod]
public async Task OneReservationLeaseExistsPerTarget()
{
    await context.ReservationLeases.AddAsync(new ReservationLease
    {
        TargetServerId = serverId,
        OperationId = operationA,
        ExpiresAt = expiresAt,
    });
    await context.SaveChangesAsync();
    await Assert.ThrowsExceptionAsync<DbUpdateException>(() => SaveLeaseAsync(serverId, operationB));
}
~~~

- [ ] **Step 2: Run Core infrastructure tests and confirm RED**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "CoreAuthenticationTests|PostgresPersistenceTests"

Expected: compile failure because Core host, DbContext and ReservationLease are absent.

- [ ] **Step 3: Implement options, auth, entities and migration**

~~~xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
<PackageReference Include="Docker.DotNet" Version="3.125.15" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.3"><PrivateAssets>all</PrivateAssets></PackageReference>
~~~

~~~csharp
public sealed class ReservationLease
{
    public Guid TargetServerId { get; set; }
    public Guid OperationId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
~~~

Register the static Bearer authentication handler, authorization, DbContext and startup migration. Add TargetServer, WarmupAgent, WarmupAttempt, LobbyOperationAudit, ReservationLease and SharedLibraryMaintenanceLease.

- [ ] **Step 4: Run authentication and PostgreSQL tests**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "CoreAuthenticationTests|PostgresPersistenceTests"

Expected: 401 without/with wrong token, 200 with correct token, and PostgreSQL rejects a duplicate reservation lease.

- [ ] **Step 5: Commit Core infrastructure**

~~~text
git add src/L4d2MatchmakingCore L4d2MatchmakingManager.sln
git commit -m "feat(core): 增加认证和 PostgreSQL 持久化基础"
~~~

### Task 7: Add target-server management API and endpoint parser

**Files:**
- Create: src/L4d2MatchmakingCore/Servers/TargetServerEndpointParser.cs
- Create: src/L4d2MatchmakingCore/Servers/TargetServerService.cs
- Create: src/L4d2MatchmakingCore/Servers/TargetServerEndpoints.cs
- Create: src/L4d2MatchmakingCore/Servers/TargetServerDtos.cs
- Create: src/L4d2MatchmakingCore/tests/TargetServerEndpointParserTests.cs
- Create: src/L4d2MatchmakingCore/tests/TargetServerEndpointTests.cs

**Why this task exists:** The API must maintain hostname or IPv4 targets with defaults priority 0, normal concurrency 36, attempt window 720 and player target 6.

**Impact / Compatibility:** Reject URLs, IPv6, whitespace, embedded credentials and invalid ports. Preserve hostnames for later IPv4 resolution. Reservation targets always have effective concurrency 1.

**Verification:** dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter TargetServer.

- [ ] **Step 1: Write failing parser and API-default tests**

~~~csharp
[DataTestMethod]
[DataRow("example.org", "example.org", (ushort)27015)]
[DataRow("203.0.113.7:28015", "203.0.113.7", (ushort)28015)]
public void ParseAcceptsHostOrIpv4(string input, string host, ushort port) =>
    Assert.AreEqual(new TargetServerAddress(host, port), TargetServerEndpointParser.Parse(input));
~~~

- [ ] **Step 2: Run target-server tests and confirm RED**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter TargetServer

Expected: compile failure because TargetServerEndpointParser does not exist.

- [ ] **Step 3: Implement authenticated CRUD and validation**

~~~csharp
app.MapGroup("/v1/servers").RequireAuthorization()
   .MapPost("/", TargetServerEndpoints.Create)
   .MapGet("/", TargetServerEndpoints.List)
   .MapPut("/{serverId:guid}", TargetServerEndpoints.Update)
   .MapDelete("/{serverId:guid}", TargetServerEndpoints.Delete);
~~~

CreateTargetServerRequest applies the confirmed defaults in TargetServerService and records each mutation in LobbyOperationAudit.

- [ ] **Step 4: Run target-server API tests**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter TargetServer

Expected: endpoint normalization, defaults, CRUD, authorization and malformed input handling pass.

- [ ] **Step 5: Commit server configuration**

~~~text
git add src/L4d2MatchmakingCore
git commit -m "feat(core): 增加目标服务器配置接口"
~~~

### Task 8: Manage local Agent Docker lifecycle and internal clients

**Files:**
- Create: src/L4d2MatchmakingCore/Agents/IAgentContainerRuntime.cs
- Create: src/L4d2MatchmakingCore/Agents/DockerAgentContainerRuntime.cs
- Create: src/L4d2MatchmakingCore/Agents/AgentControlClient.cs
- Create: src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs
- Create: src/L4d2MatchmakingCore/Agents/WarmupAgentEndpoints.cs
- Create: src/L4d2MatchmakingCore/tests/DockerAgentContainerRuntimeTests.cs
- Create: src/L4d2MatchmakingCore/tests/WarmupAgentEndpointTests.cs

**Why this task exists:** Core provisions one container per Steam account with an independent credential volume and optional region, while blocking arbitrary Docker injection.

**Impact / Compatibility:** Only containers labelled com.l4d2.matchmaking.managed=true may be managed. Delete retains steam-data and agent-config volumes. Port 8080 remains unbound; noVNC maps only to 127.0.0.1 in the configured range.

**Verification:** dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupAgent|DockerAgent".

- [ ] **Step 1: Write failing container-definition tests**

~~~csharp
[TestMethod]
public async Task CreateUsesOnlyTheManagedContainerDefinition()
{
    await runtime.CreateAsync(agent, CancellationToken.None);
    Assert.AreEqual("l4d2-steam-lobby-agent:local", fake.Image);
    Assert.AreEqual("127.0.0.1", fake.NoVncBinding.HostIp);
    Assert.IsFalse(fake.PublishedPorts.Contains(8080));
    CollectionAssert.Contains(fake.Volumes, "steam-data-" + agent.Id);
    CollectionAssert.Contains(fake.Volumes, "agent-config-" + agent.Id);
    Assert.IsTrue(fake.BindMounts.Any(m =>
        m.Source == options.SharedLibraryHostPath && m.Target == "/mnt/steam-library"));
}
~~~

- [ ] **Step 2: Run Agent lifecycle tests and confirm RED**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupAgent|DockerAgent"

Expected: compile failure because IAgentContainerRuntime does not exist.

- [ ] **Step 3: Implement Docker runtime, lifecycle endpoints and Agent client**

~~~csharp
public interface IAgentControlClient
{
    Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken ct);
    Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken ct);
    Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken ct);
    Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken ct);
    Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken ct);
}

public interface IHealthyAgentSelector
{
    Task<WarmupAgent?> SelectAsync(CancellationToken ct);
}
~~~

Use Docker.DotNet only in DockerAgentContainerRuntime. Core options own the image, shared-library host path, network, noVNC range and internal Agent port. Every managed container receives the configured shared-library bind mount and exactly its own steam-data/account-config volumes; account data volumes are never reused across Agent IDs. Map authenticated create/list/update/start/stop/recreate/delete routes.

- [ ] **Step 4: Run lifecycle tests**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupAgent|DockerAgent"

Expected: fake Docker checks labels, volumes and ports; API responses contain no secrets.

- [ ] **Step 5: Commit Agent lifecycle**

~~~text
git add src/L4d2MatchmakingCore
git commit -m "feat(core): 管理本机 Steam Agent 容器"
~~~

### Task 9: Add A2S observation and Agent-provided lobby query proxy

**Files:**
- Create: src/L4d2MatchmakingCore/A2s/ISourceA2sClient.cs
- Create: src/L4d2MatchmakingCore/A2s/SourceA2sClient.cs
- Create: src/L4d2MatchmakingCore/A2s/A2sServerInfo.cs
- Create: src/L4d2MatchmakingCore/Lobbies/LobbyQueryEndpoints.cs
- Create: src/L4d2MatchmakingCore/tests/SourceA2sClientTests.cs
- Create: src/L4d2MatchmakingCore/tests/LobbyQueryEndpointTests.cs

**Why this task exists:** A2S player counts govern start/stop decisions. The public query API must obtain arbitrary lobby metadata and members from a healthy Agent.

**Impact / Compatibility:** Source A2S_INFO challenge packets are handled once per query; a timeout is a fresh observation failure, not reuse of old player counts. Core responses hide the selected Agent identity.

**Verification:** dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "A2s|LobbyQuery".

- [ ] **Step 1: Write failing A2S challenge and query-proxy tests**

~~~csharp
[TestMethod]
public async Task GetInfoRepliesToChallengeAndParsesPlayers()
{
    await using var server = await FakeA2sServer.StartAsync(challenge: 1234, playerCount: 4);
    var info = await client.GetInfoAsync(server.Endpoint, CancellationToken.None);
    Assert.AreEqual(4, info.PlayerCount);
    Assert.AreEqual(2, server.RequestCount);
}
~~~

- [ ] **Step 2: Run A2S/query tests and confirm RED**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "A2s|LobbyQuery"

Expected: compile failure because ISourceA2sClient and route are absent.

- [ ] **Step 3: Implement bounded UDP query and authenticated proxy route**

~~~csharp
public interface ISourceA2sClient
{
    Task<A2sServerInfo> GetInfoAsync(IPEndPoint endpoint, CancellationToken ct);
}
~~~

~~~csharp
app.MapGet("/v1/lobbies/{lobbyId}", async (string lobbyId, IHealthyAgentSelector selector, IAgentControlClient agents, CancellationToken ct) =>
{
    var agent = await selector.SelectAsync(ct);
    return agent is null
        ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        : Results.Ok(await agents.ReadLobbyAsync(agent, lobbyId, ct));
}).RequireAuthorization();
~~~

Resolve hostnames to IPv4 before both A2S and Agent operation requests. Map unavailable Agent and A2S dependencies to stable 503/504 errors without raw output.

- [ ] **Step 4: Run A2S and lobby-query tests**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "A2s|LobbyQuery"

Expected: challenge flow, player count, Agent query during active operation, 401 and no Agent leakage pass.

- [ ] **Step 5: Commit real-time observation**

~~~text
git add src/L4d2MatchmakingCore
git commit -m "feat(core): 增加 A2S 观测与大厅查询代理"
~~~

### Task 10: Implement the pure warm-up decision engine

**Files:**
- Create: src/L4d2MatchmakingCore/Scheduling/WarmupState.cs
- Create: src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs
- Create: src/L4d2MatchmakingCore/Scheduling/SchedulingModels.cs
- Create: src/L4d2MatchmakingCore/Scheduling/IClock.cs
- Create: src/L4d2MatchmakingCore/tests/WarmupDecisionEngineTests.cs

**Why this task exists:** The reservation and normal workflows combine A2S, 120/30 second timers, 12-minute parent attempts, priority, exclusivity and capacity. A pure engine makes them deterministic.

**Impact / Compatibility:** Reservation creation skips targets where A2S players are greater than zero. A2S >= player target or the 12-minute deadline ends either attempt. No RCON outcome participates in decisions.

**Verification:** dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter WarmupDecisionEngineTests.

- [ ] **Step 1: Write all key transition tests**

~~~csharp
[TestMethod]
public void ReservedTargetWithPlayersSkipsBeforeLease() =>
    Assert.AreEqual(WarmupDecision.SkipTarget, engine.Evaluate(reservedSelecting, 1, now));

[TestMethod]
public void ReservedNoMemberFor120SecondsRecreatesSameTarget() =>
    Assert.AreEqual(WarmupDecision.RecreateSameTarget, engine.Evaluate(reservedWaiting, 0, now));

[TestMethod]
public void NormalQuietFor30SecondsRecreatesSameTarget() =>
    Assert.AreEqual(WarmupDecision.RecreateSameTarget, engine.Evaluate(normalQuiet, 0, now));
~~~

- [ ] **Step 2: Run engine tests and confirm RED**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter WarmupDecisionEngineTests

Expected: compile failure because WarmupDecisionEngine is absent.

- [ ] **Step 3: Implement immutable decisions and member deltas**

~~~csharp
public enum WarmupDecision { Continue, RecreateSameTarget, ReleaseAndReschedule, SkipTarget, QuarantineAgent }

public WarmupDecision Evaluate(TargetServer server, int a2sPlayers, WarmupAttemptSnapshot attempt, DateTimeOffset now)
{
    if (server.RequiresReservation && attempt.Phase == WarmupPhase.Selecting && a2sPlayers > 0) return WarmupDecision.SkipTarget;
    if (a2sPlayers >= server.PlayerTarget || now >= attempt.Deadline) return WarmupDecision.ReleaseAndReschedule;
    if (server.RequiresReservation && attempt.FirstExternalMemberAt is null && now >= attempt.LobbyReadyAt + TimeSpan.FromSeconds(120)) return WarmupDecision.RecreateSameTarget;
    if (attempt.QuietSince is { } quiet && now >= quiet + TimeSpan.FromSeconds(30)) return WarmupDecision.RecreateSameTarget;
    return WarmupDecision.Continue;
}
~~~

Track previous member sets excluding owner; every external join resets QuietSince. Clamp reservation capacity to one and choose descending priority with a persisted round-robin cursor among ties.

- [ ] **Step 4: Run the full engine matrix**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter WarmupDecisionEngineTests

Expected: all timer, skip, target, priority, exclusive and 36-capacity tests pass.

- [ ] **Step 5: Commit scheduling rules**

~~~text
git add src/L4d2MatchmakingCore/Scheduling src/L4d2MatchmakingCore/tests/WarmupDecisionEngineTests.cs
git commit -m "feat(core): 实现暖服状态机与调度约束"
~~~

### Task 11: Connect persistence, leases, recovery and the scheduler service

**Files:**
- Create: src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs
- Create: src/L4d2MatchmakingCore/Scheduling/WarmupAttemptRepository.cs
- Create: src/L4d2MatchmakingCore/Scheduling/ReservationLeaseRepository.cs
- Create: src/L4d2MatchmakingCore/Scheduling/SharedLibraryMaintenanceService.cs
- Create: src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs
- Create: src/L4d2MatchmakingCore/tests/WarmupRecoveryIntegrationTests.cs

**Why this task exists:** The pure engine needs one resilient owner that observes live dependencies, makes idempotent Agent calls and never reuses uncertain accounts or reservation targets.

**Impact / Compatibility:** An Agent has at most one active operation. Database rows record attempt/lease/audit only. On restart, Core must query Agent snapshots before releasing any lease. A shared-library maintenance lock stops scheduling.

**Repair Track:** Retire docker-exec from the Core business path; IAgentControlClient and WarmupSchedulerService become the only runtime owners.

**Retirement Track:** Keep docker-exec documentation only as historical diagnostic evidence until Agent HTTP is accepted on Ubuntu, then delete its temporary adapter section.

**Verification:** dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupScheduler|WarmupRecovery".

- [ ] **Step 1: Write failing scheduler/recovery/maintenance tests**

~~~csharp
[TestMethod]
public async Task RestartKeepsLeaseUntilAgentSnapshotIsReconciled()
{
    repository.AddRunningAttempt(attempt);
    agentClient.OperationSnapshot = activeSnapshot;
    await scheduler.RecoverAsync(CancellationToken.None);
    Assert.IsTrue(await repository.HasReservationLeaseAsync(attempt.TargetServerId));
}

[TestMethod]
public async Task MaintenanceLockPreventsNewScheduling()
{
    await maintenance.AcquireAsync(CancellationToken.None);
    await scheduler.TickAsync(CancellationToken.None);
    Assert.AreEqual(0, agentClient.StartCalls);
}
~~~

- [ ] **Step 2: Run scheduler tests and confirm RED**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupScheduler|WarmupRecovery"

Expected: compile failure because WarmupSchedulerService is absent.

- [ ] **Step 3: Implement recovery-first background service**

~~~csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await RecoverAsync(stoppingToken);
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
    while (await timer.WaitForNextTickAsync(stoppingToken))
        await TickAsync(stoppingToken);
}
~~~

Tick first skips shared-library maintenance, then reads running Agent snapshots and A2S, evaluates decisions, stops lobbies before recreating them, and writes lease changes transactionally. New starts create a UUID operation ID before calling Agent. Agent uncertainty quarantines the account and preserves reservation exclusion.

- [ ] **Step 4: Run scheduler, recovery and lease tests**

Run: dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "WarmupScheduler|WarmupRecovery"

Expected: no duplicate reservation, no normal target beyond 36 operations, no Agent with two operations, restart favors live Agent data and maintenance prevents starts.

- [ ] **Step 5: Commit scheduler execution**

~~~text
git add src/L4d2MatchmakingCore
git commit -m "feat(core): 持久化执行暖服调度与恢复"
~~~

### Task 12: Package Compose, update documentation and verify the real journey

**Files:**
- Create: deploy/matchmaking-core/Dockerfile
- Create: deploy/matchmaking-core/docker-compose.yml
- Create: deploy/matchmaking-core/.env.example
- Create: deploy/matchmaking-core/Test-ComposeContract.ps1
- Modify: docs/steam-lobby-agent-api.md
- Modify: docs/steam-lobby-automation.md
- Modify: deploy/steam-lobby-agent/README.md
- Create: docs/matchmaking-core-api.md

**Why this task exists:** Operators need one safe Core/PostgreSQL deployment and exact procedures for Agent creation, shared-library maintenance, noVNC login and authenticated APIs.

**Impact / Compatibility:** Core alone mounts /var/run/docker.sock. PostgreSQL has no host port. Dynamic Agent port 8080 is not published. noVNC is loopback only. Documentation must state reservation success is Agent observation, not independent server verification.

**Verification:** pwsh deploy/matchmaking-core/Test-ComposeContract.ps1; docker compose -f deploy/matchmaking-core/docker-compose.yml config; dotnet test L4d2MatchmakingManager.sln.

- [ ] **Step 1: Write failing Core Compose boundary tests**

~~~powershell
if ($compose -notmatch '/var/run/docker.sock:/var/run/docker.sock') {
    throw 'Only Core may mount the Docker socket.'
}
if ($compose -notmatch 'postgres:') {
    throw 'Core deployment requires PostgreSQL.'
}
if ($compose -match '5432:5432') {
    throw 'PostgreSQL must not publish a host port.'
}
if ($compose -notmatch 'CORE_API_TOKEN_FILE') {
    throw 'Core must read the management token from a secret file.'
}
~~~

- [ ] **Step 2: Run the Compose contract test and confirm RED**

Run: pwsh deploy/matchmaking-core/Test-ComposeContract.ps1

Expected: failure because deploy/matchmaking-core/docker-compose.yml is absent.

- [ ] **Step 3: Add deployment and update public contracts**

~~~yaml
services:
  core:
    build:
      context: ../..
      dockerfile: deploy/matchmaking-core/Dockerfile
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
  postgres:
    image: postgres:17
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U matchmaking"]
~~~

Document exact token-authenticated server and Agent APIs, noVNC loopback access, shared library maintenance and GET /v1/lobbies/{id}. Replace old per-request Probe process wording with actor semantics and remove docker-exec from the Core business instructions.

- [ ] **Step 4: Run static deployment and full automated verification**

Run: pwsh deploy/matchmaking-core/Test-ComposeContract.ps1
Run: docker compose -f deploy/matchmaking-core/docker-compose.yml config
Run: dotnet test L4d2MatchmakingManager.sln
Run: pwsh deploy/steam-lobby-agent/Test-ComposeContract.ps1
Run: pwsh deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1 -SharedLibraryPath deploy/steam-lobby-agent/tests/fixtures/shared-library

Expected: all contracts, Compose syntax and automated tests pass.

- [ ] **Step 5: Execute Ubuntu acceptance and record evidence**

~~~text
1. Start Core/PostgreSQL, create an Agent, reach loopback noVNC and complete Steam login.
2. Under the shared-library maintenance lock install AppID 550; assert libsteam_api.so exists and AutoUpdateBehavior is 1.
3. Verify unique credential volumes and shared game content across two Agents.
4. Verify reservation A2S>0 skip, empty-server creation, 120-second recreate, first-member 30-second exit and priority reschedule.
5. Verify normal cap 36, 30-second recreate, A2S>=6 stop and 12-minute exit.
6. Query an active and a non-owned lobby through GET /v1/lobbies/{id}.
7. Restart Core and verify no lease releases until Agent snapshots reconcile.
~~~

- [ ] **Step 6: Commit deployment and documentation**

~~~text
git add deploy/matchmaking-core deploy/steam-lobby-agent docs
git commit -m "feat(deploy): 提供单主机暖服核心部署"
~~~

## Plan Self-Review

- Coverage: Task 2 covers C1-C14; Tasks 3-5 cover actor, Agent APIs, arbitrary reads, shared content, region, shader and AppID 550 update policy; Tasks 6-11 cover authentication, storage, Docker, A2S, both warm-up loops, leases, recovery and priority; Task 12 covers deployment and the full operator journey.
- Compatibility: Tasks 3-4 preserve health routes and stable payloads. Task 11 retires only the Core docker-exec business path while retaining Probe CLI diagnostics.
- Runtime authority: Tasks 9-11 require fresh A2S and Agent reads at every decision.
- Residual risk: Steam native actor behavior, download region behavior and noVNC must be accepted on a real Ubuntu host. Without RCON, reservation success is Agent observation only.
