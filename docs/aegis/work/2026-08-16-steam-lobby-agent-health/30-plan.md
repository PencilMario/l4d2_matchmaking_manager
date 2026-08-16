# Steam Lobby Agent Health Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build one Ubuntu Docker Agent that retains a Steam Desktop login and exposes a fresh, read-only health result produced by `SteamLobbyProbe`.

**Architecture:** Add a non-mutating `health-check` command to `SteamLobbyProbe`, emitted as a small JSON contract. A new ASP.NET Core Agent serializes command execution, combines it with a Steam Desktop process check, and returns `/healthz` and `/v1/probe/status`. Docker runs Steam Desktop in Xvfb and preserves its user directory in an account-specific volume.

**Tech Stack:** .NET 8 Probe, .NET 10 ASP.NET Core Minimal API, MSTest, Ubuntu 24.04, Docker Compose, Steam Desktop, Xvfb and noVNC.

**Baseline / Authority Refs:** `20-spec.md`, `10-baseline-readset.md`, `research/SteamLobbyProbe/README.md`, `research/SteamLobbyProbe/Program.cs`.

**Compatibility Boundary:** Existing Probe CLI modes, reservation vectors and Windows `win-x86` publishing remain available. The new check must not call Create/Join/Leave/Set lobby APIs. No state snapshot is persisted by the Agent or a database.

**Verification:** Unit and HTTP integration tests; full existing protocol regression; Release builds; Linux publish of Probe and Agent; Docker image build; remote Docker deployment and three fresh authenticated Steam checks.

---

### Task 1: Read-only Probe Health Contract

**Files:**
- Modify: `research/SteamLobbyProbe/SteamLobbyProbe.csproj`
- Modify: `research/SteamLobbyProbe/Program.cs`
- Create: `research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj`
- Create: `research/SteamLobbyProbe/tests/HealthCheckCommandTests.cs`

**Why this task exists:** The existing default command mutates Steam by creating a lobby. The Agent needs a direct Probe-owned, machine-readable check that safely runs repeatedly.

**Repair Track:** The canonical owner of the Steam API health check is `Program.TryRunHealthCheck`; it reuses the existing private `SteamApi` binding and ManualDispatch wait path without widening that binding's visibility. Existing diagnostic commands remain unchanged.

**Retirement Track:** Text scraping of Probe console output is not introduced. The Agent consumes only the JSON contract; no legacy health fallback exists.

**Verification:** `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --nologo`; `dotnet publish research/SteamLobbyProbe/SteamLobbyProbe.csproj -c Release -r linux-x64 --self-contained true --nologo`.

- [ ] **Step 1: Write the failing process-contract test**

```csharp
[TestMethod]
public async Task HealthCheckWithMissingLibraryEmitsMachineReadableFailure()
{
    var result = await ProbeProcess.RunAsync("missing.so", "health-check", "--json");
    Assert.AreNotEqual(0, result.ExitCode);
    Assert.AreEqual("steam_api_load_failed", JsonDocument.Parse(result.StandardOutput)
        .RootElement.GetProperty("failure").GetString());
}
```

- [ ] **Step 2: Run the test and observe RED**

Run: `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --nologo`

Expected: failure because `health-check` and its JSON contract do not exist.

- [ ] **Step 3: Implement the minimum command**

```csharp
if (HealthCheckCommand.TryRun(args, out var healthExitCode))
    return healthExitCode;
```

`HealthCheckCommand` loads the supplied native library, calls Init, ManualDispatch, GetAppID, BLoggedOn, GetSteamID and RequestLobbyList, waits only for the list callback, emits one JSON result and always calls Shutdown/NativeLibrary.Free. It catches native loading and callback failures as stable error codes.

- [ ] **Step 4: Run health tests and existing protocol regression**

Run: `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --nologo`

Run: `dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo`

Expected: both pass; the process test proves a missing library is safe and machine-readable.

- [ ] **Step 5: Verify Linux publish compatibility**

Run: `dotnet publish research/SteamLobbyProbe/SteamLobbyProbe.csproj -c Release -r linux-x64 --self-contained true --nologo`

Expected: successful publish with no fixed x86 platform error.

### Task 2: HTTP Agent With Fresh Serialized Checks

**Files:**
- Create: `src/L4d2LobbyAgent/L4d2LobbyAgent.csproj`
- Create: `src/L4d2LobbyAgent/Program.cs`
- Create: `src/L4d2LobbyAgent/Probe/ProbeContracts.cs`
- Create: `src/L4d2LobbyAgent/Probe/ProbeCommandRunner.cs`
- Create: `src/L4d2LobbyAgent/Probe/ProbeStatusService.cs`
- Create: `src/L4d2LobbyAgent/Probe/SteamDesktopDetector.cs`
- Create: `src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj`
- Create: `src/L4d2LobbyAgent/tests/ProbeStatusServiceTests.cs`
- Create: `src/L4d2LobbyAgent/tests/ProbeStatusEndpointTests.cs`

**Why this task exists:** One account must not have concurrent Steam API helper processes. The Agent turns the Probe contract into an operational endpoint without persisting status.

**Impact / Compatibility:** Each `/v1/probe/status` request starts a new Probe process under a semaphore and returns a newly observed timestamp. `/healthz` remains a process-liveness response and never launches Probe.

**Verification:** `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --nologo`; `dotnet build src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -c Release --nologo`.

- [ ] **Step 1: Write the failing service tests**

```csharp
[TestMethod]
public async Task StatusCheckRunsTheProbeForEveryRequestAndSerializesCalls()
{
    var runner = new BlockingFakeRunner();
    var service = new ProbeStatusService(runner, new FakeDesktopDetector(true));
    await Task.WhenAll(service.GetAsync(CancellationToken.None), service.GetAsync(CancellationToken.None));
    Assert.AreEqual(2, runner.CallCount);
    Assert.AreEqual(1, runner.MaximumConcurrency);
}
```

- [ ] **Step 2: Run and observe RED**

Run: `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --nologo`

Expected: compile failure because Agent project and `ProbeStatusService` do not exist.

- [ ] **Step 3: Implement contracts, runner and serialization**

```csharp
public sealed class ProbeStatusService(IProbeCommandRunner runner, ISteamDesktopDetector desktop)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<ProbeStatusResponse> GetAsync(CancellationToken cancellationToken) { /* fresh runner call */ }
}
```

The process runner invokes `STEAM_LOBBY_PROBE_PATH <STEAM_API_LIBRARY_PATH> health-check --json`, parses strict JSON, times out via cancellation and redacts paths/stdout from responses.

- [ ] **Step 4: Add Minimal API endpoint tests before endpoint code**

```csharp
[TestMethod]
public async Task ProbeEndpointReturnsServiceUnavailableForFailedFreshCheck()
{
    using var app = AgentApplicationFactory.With(new FakeRunner(ProbeResult.Failure("steam_not_logged_on")));
    var response = await app.Client.GetAsync("/v1/probe/status");
    Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
}
```

- [ ] **Step 5: Implement the endpoints and run GREEN**

`GET /healthz` returns `{ "status": "alive" }`. `GET /v1/probe/status` uses `ProbeStatusService`, returns HTTP 200 only when every check is ready, otherwise HTTP 503 with a stable non-sensitive failure code.

Run: `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --nologo`

Expected: service and endpoint tests pass, including fresh request count and 503 behavior.

### Task 3: Ubuntu Container and Persistent Steam Login

**Files:**
- Create: `deploy/steam-lobby-agent/Dockerfile`
- Create: `deploy/steam-lobby-agent/entrypoint.sh`
- Create: `deploy/steam-lobby-agent/docker-compose.yml`
- Create: `deploy/steam-lobby-agent/.env.example`
- Create: `deploy/steam-lobby-agent/README.md`
- Create: `.dockerignore`

**Why this task exists:** The Agent needs Steam Desktop in the same persistent Linux user directory as the Probe, while keeping noVNC and HTTP off public interfaces by default.

**Impact / Compatibility:** Docker volumes retain Steam login data and are never mounted into the build context. The image runs no game client and does not install or use Wine.

**Verification:** `docker compose -f deploy/steam-lobby-agent/docker-compose.yml config`; `docker build -f deploy/steam-lobby-agent/Dockerfile -t l4d2-steam-lobby-agent:local .`.

- [ ] **Step 1: Write the failing deployment contract test**

```powershell
$compose = Get-Content -Raw deploy/steam-lobby-agent/docker-compose.yml
if ($compose -notmatch 'steam-data:') { throw 'Missing persistent Steam volume.' }
if ($compose -notmatch '127.0.0.1:6080:6080') { throw 'noVNC must bind to loopback.' }
if ($compose -match 'left4dead2') { throw 'The image must not start the L4D2 client.' }
```

- [ ] **Step 2: Run the contract test and observe RED**

Run: `pwsh -File deploy/steam-lobby-agent/Test-ComposeContract.ps1`

Expected: file-not-found failure because deployment files do not exist.

- [ ] **Step 3: Implement the image and Compose definition**

Build the Probe for `linux-x64` in a .NET SDK stage, publish the Agent self-contained, install Steam Desktop/Xvfb/noVNC in Ubuntu, and start Steam under the non-root `steam` user. Compose binds ports to `127.0.0.1`, mounts `steam-data` at `/home/steam`, and uses an explicit `STEAM_API_LIBRARY_PATH` supplied by `.env`.

- [ ] **Step 4: Run contract validation and image build**

Run: `pwsh -File deploy/steam-lobby-agent/Test-ComposeContract.ps1`

Run: `docker compose -f deploy/steam-lobby-agent/docker-compose.yml config`

Run: `docker build -f deploy/steam-lobby-agent/Dockerfile -t l4d2-steam-lobby-agent:local .`

Expected: all succeed; no public host binding or game client command appears.

### Task 4: Local Regression and Remote Docker Validation

**Files:**
- Create: `docs/aegis/work/2026-08-16-steam-lobby-agent-health/50-evidence.md`
- Modify: `docs/aegis/work/2026-08-16-steam-lobby-agent-health/40-atomic-tasks.md`

**Why this task exists:** A synthetic process test cannot establish a real Steam Desktop/L4D2 API session. Deployment must distinguish image success, login readiness and Probe callback evidence.

**Verification:** full .NET test suite, Release builds, Docker image build and three remote calls to `/v1/probe/status` after Steam login.

- [ ] **Step 1: Run the full local verification bundle**

Run:

```powershell
dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo
dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --nologo
dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --nologo
dotnet build research/SteamLobbyProbe/SteamLobbyProbe.csproj -c Release --nologo
dotnet build src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -c Release --nologo
```

Expected: all pass with zero warnings/errors.

- [ ] **Step 2: Deploy only after inspecting remote Docker and SSH prerequisites**

Run read-only SSH checks for OS, Docker, disk, memory and the destination directory. Upload the deployment set, run Compose, and do not expose noVNC or HTTP publicly.

- [ ] **Step 3: Complete the authenticated live check**

Use a private SSH tunnel to noVNC for first login if needed. Install/locate the owned AppID 550 Linux Steam API library, set `STEAM_API_LIBRARY_PATH`, then call `/v1/probe/status` three times. Record only check codes and timestamps, never tokens or paths.

- [ ] **Step 4: Record evidence and rollback instructions**

Write test counts, builds, container IDs, HTTP codes and redacted live check results to `50-evidence.md`. Rollback is `docker compose down` without `-v`; this preserves Steam login volume. `down -v` is forbidden unless the user explicitly requests credential removal.
