# Reliable Lobby Query Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Return callback-confirmed lobby metadata and, where safe, actual target-lobby members excluding the temporary query Agent.

**Architecture:** `SteamNativeRuntime` owns matching Steam callback waits and native temporary membership. `SteamSessionActor` serializes that lifecycle with a held warm-up operation, filters its own Steam ID, and marks a lost standard operation failed. Core orders healthy Agents idle then active standard, holds a per-Agent query lease, and quarantines a failed-preservation Agent.

**Tech Stack:** .NET 8, ASP.NET Core Minimal API, MSTest, EF Core, Steam Manual Dispatch.

**Baseline / Authority Refs:** `CONTEXT.md`; `20-spec.md`; `AgentContracts.cs`; Core lobby endpoint/client/scheduler/data; Agent endpoint/service; Probe actor/runtime; both API documents.

**Compatibility Boundary:** The existing `GET /v1/lobbies/{lobbyId}` path, authorization and existing snapshot fields stay stable. `memberDataStatus` is additive. `metadata["Members:numPlayers"]` is Steam owner metadata and is not changed. Reservation or uncertain operations are never temporarily joined.

**Verification:** Focused Probe, Agent and Core MSTest suites, builds of all changed production projects, then a controlled deployed standard-lobby preservation check.

---

## Facts, Assumptions, Retirement

- `RequestLobbyData` returns only a Boolean; callback 505 (`LobbyDataUpdate_t`) is its completion signal. Reading before its matching successful callback caused false empty snapshots.
- `JoinLobby` completes through callback 504 (`LobbyEnter_t`); target member enumeration requires target membership.
- The Core database is selection authority for `WarmupAttempt.Mode` and `.State`; the actor repeats this safety check on its own command thread.
- The live test must verify that a Steam session holding one standard lobby can join/leave a second lobby and still remains in the first. A failure is quarantine, not automatic retry.
- Repair: replace the ten-times-25-ms cache pump and direct Core proxy. Retirement: no successful fallback remains for unconfirmed metadata; use `503 "lobby_data_unavailable"`.

### Task 1: Add the public status contract

**Files:** modify `src/L4d2Matchmaking.Contracts/AgentContracts.cs`; modify `research/SteamLobbyProbe/tests/SteamSessionActorTests.cs`.

**Why / compatibility:** Frontend callers need to know whether `members` is complete. Add a trailing default so all existing five-argument `LobbySnapshot` constructors remain valid.

**Repair Track:** Add `LobbyMemberDataStatus` constants: `complete`, `metadata_only_no_query_agent`, `metadata_only_join_denied`, `metadata_only_join_timeout`, `metadata_only_agent_state_changed`.

**Retirement Track:** Retire interpreting `members: []` as a reliable player count without a completion status.

- [ ] Write RED actor assertions:

```csharp
var result = await actor.QueryLobbyAsync(targetLobby, CancellationToken.None);
Assert.AreEqual(LobbyMemberDataStatus.Complete, result.MemberDataStatus);
CollectionAssert.DoesNotContain(result.Members.Select(x => x.SteamId).ToList(), native.CurrentSteamId.ToString());
```

- [ ] Run `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter SteamSessionActorTests` and observe compilation failure for the missing contract/method.
- [ ] Add the constants and append `string MemberDataStatus = LobbyMemberDataStatus.Complete` to `LobbySnapshot`.
- [ ] Rerun the focused test after Task 2 implements the method; expect PASS.
- [ ] Commit: `git add src/L4d2Matchmaking.Contracts/AgentContracts.cs research/SteamLobbyProbe/tests/SteamSessionActorTests.cs` then `git commit -m "feat(lobby): 增加成员查询完成状态"`.

### Task 2: Confirm metadata via callback and expose native join results

**Files:** modify `research/SteamLobbyProbe/SteamSessionContracts.cs`, `research/SteamLobbyProbe/SteamNativeRuntime.cs`, and `research/SteamLobbyProbe/tests/SteamSessionActorTests.cs`.

**Why / compatibility:** The shared runtime is the canonical Steam API owner. Normal operation snapshots remain unchanged; only arbitrary-query metadata requires the new confirmation primitive.

**Repair Track:** Remove the old fixed 250 ms data pump. Decode callback 505 with lobby ID at byte offset 0 and success at offset 16, match only the requested lobby, free every Manual Dispatch callback, and timeout after five seconds.

**Retirement Track:** Do not preserve a cache-only read branch.

- [ ] Write RED fake-runtime tests for callback timeout, join denial, join timeout, and leave-in-finally:

```csharp
native.MetadataResult = NativeLobbyMetadataResult.Timeout;
await Assert.ThrowsExceptionAsync<SteamRuntimeException>(() => actor.QueryLobbyAsync(targetLobby, CancellationToken.None));
native.JoinResult = NativeLobbyJoinResult.Timeout;
Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyJoinTimeout,
    (await actor.QueryLobbyAsync(targetLobby, CancellationToken.None)).MemberDataStatus);
```

- [ ] Run `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter SteamSessionActorTests`; expect RED because the runtime result types and actor behavior are absent.
- [ ] Add internal runtime methods for confirmed metadata, joining, reading target members, current Steam ID, leaving, and current-user membership. Implement `RequestLobbyData` as:

```csharp
if (!_api.RequestLobbyData(_matchmaking, lobbyId) || !WaitForLobbyDataUpdate(lobbyId))
    throw new SteamRuntimeException("lobby_data_unavailable");
```

Map a zero/rejected `JoinLobby` call to denied; map a pending callback past deadline to timeout; require matching callback lobby ID and enter response `1`.
- [ ] Run `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter SteamSessionActorTests` (PASS), then `dotnet build research/SteamLobbyProbe/SteamLobbyProbe.csproj --no-restore` (zero errors/warnings).
- [ ] Commit: `git add research/SteamLobbyProbe/SteamSessionContracts.cs research/SteamLobbyProbe/SteamNativeRuntime.cs research/SteamLobbyProbe/tests/SteamSessionActorTests.cs` then `git commit -m "fix(steam): 等待大厅数据回调后再读取"`.

### Task 3: Make the actor own safe temporary membership

**Files:** modify `research/SteamLobbyProbe/SteamSessionContracts.cs`, `research/SteamLobbyProbe/SteamSessionActor.cs`, and `research/SteamLobbyProbe/tests/SteamSessionActorTests.cs`.

**Why / compatibility:** Only the serialized actor can reliably observe a changed operation while ensuring cleanup. Active reservation and uncertain operations never call native `JoinLobby`.

**Repair Track:** Add `QueryLobbyAsync`; refresh metadata first; reject unsafe state with `metadata_only_agent_state_changed`; join only for idle/standard states; remove current Steam ID from target members.

**Retirement Track:** Retire trusting Core's stale selection as sufficient authorization for a temporary join.

- [ ] Write RED state-branch tests:

```csharp
await actor.StartAsync(ReservedOperation(), CancellationToken.None);
var snapshot = await actor.QueryLobbyAsync(otherLobby, CancellationToken.None);
Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyAgentStateChanged, snapshot.MemberDataStatus);
Assert.AreEqual(0, native.JoinLobbyCalls);
native.PreserveOriginalLobby = false;
await Assert.ThrowsExceptionAsync<SteamRuntimeException>(() => actor.QueryLobbyAsync(otherLobby, CancellationToken.None));
Assert.AreEqual("failed", (await actor.GetOperationAsync(operationId, CancellationToken.None))!.State);
```

- [ ] Run `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter SteamSessionActorTests`; expect RED.
- [ ] Track active operation mode. For a standard query use `try/finally { LeaveLobby(target); }`; after leave require `IsCurrentUserLobbyMember(originalLobby, self)`. On failure store operation `State = "failed"`, clear active operation, and throw `SteamRuntimeException("lobby_operation_preservation_failed")`. If target equals held lobby, read members and self-filter without leaving it.
- [ ] Run the same focused test; expect PASS and assertions proving a leave happens for success and a thrown member read.
- [ ] Commit: `git add research/SteamLobbyProbe/SteamSessionContracts.cs research/SteamLobbyProbe/SteamSessionActor.cs research/SteamLobbyProbe/tests/SteamSessionActorTests.cs` then `git commit -m "feat(lobby): 安全临时加入并过滤查询账号"`.

### Task 4: Expose the reliable Agent query route

**Files:** modify `src/L4d2LobbyAgent/Steam/AgentSteamSessionService.cs`, `src/L4d2LobbyAgent/Program.cs`, and `src/L4d2LobbyAgent/tests/AgentLobbyEndpointTests.cs`.

**Why / compatibility:** Core needs a single private route that invokes actor query behavior. Valid input still receives the `LobbySnapshot` JSON shape; invalid IDs retain `400`.

**Repair Track:** Add `QueryLobbyAsync` to Agent service and actor interface. Map only stable known runtime failures at the endpoint.

**Retirement Track:** Retire direct use of `ReadLobbyAsync` for arbitrary public query requests.

- [ ] Write RED route tests:

```csharp
service.QueryException = new SteamRuntimeException("lobby_data_unavailable");
var response = await client.GetAsync("/v1/lobbies/109775242170052468");
Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
Assert.AreEqual("\"lobby_data_unavailable\"", await response.Content.ReadAsStringAsync());
```

Also assert serialized JSON has `memberDataStatus: "complete"` and that preservation failure is an identifiable non-success for Core.
- [ ] Run `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --filter AgentLobbyEndpointTests`; expect RED.
- [ ] Forward `QueryLobbyAsync` to the actor. Return `Results.Json("lobby_data_unavailable", statusCode: 503)` for metadata failure and a stable preservation-failure body/status; let unknown exceptions follow normal ASP.NET handling.
- [ ] Run focused Agent tests (PASS) and `dotnet build src/L4d2LobbyAgent/L4d2LobbyAgent.csproj --no-restore` (zero errors/warnings).
- [ ] Commit: `git add src/L4d2LobbyAgent/Steam/AgentSteamSessionService.cs src/L4d2LobbyAgent/Program.cs src/L4d2LobbyAgent/tests/AgentLobbyEndpointTests.cs` then `git commit -m "feat(agent): 暴露可靠大厅查询结果"`.

### Task 5: Select, lease, proxy and quarantine in Core

**Files:** modify `src/L4d2MatchmakingCore/Agents/AgentControlClient.cs`, `src/L4d2MatchmakingCore/Lobbies/LobbyQueryEndpoints.cs`, `src/L4d2MatchmakingCore/Program.cs`, `src/L4d2MatchmakingCore/tests/LobbyQueryEndpointTests.cs`; create `src/L4d2MatchmakingCore/Lobbies/LobbyQueryService.cs`.

**Why / compatibility:** Core must minimize disruption across tens or hundreds of Agents and turn preservation failure into a durable safety action.

**Repair Track:** `LobbyQueryService` asks `ListHealthyAsync`, loads current active/uncertain attempts, chooses idle before active `mode=standard,state=active`, and acquires an in-process `ConcurrentDictionary<Guid, SemaphoreSlim>` lease before the Agent HTTP call. If neither membership-query candidate exists, it sends a metadata-only query to another healthy Agent and returns `metadata_only_no_query_agent`. Every lease is released in `finally`.

**Retirement Track:** Retire `IHealthyAgentSelector.SelectAsync` as the one-step direct proxy choice for lobby queries. Reservation/uncertain candidates do not receive a membership lease.

- [ ] Write RED integration tests with in-memory attempts:

```csharp
await AddAttempt(reserved.Id, "reserved", "active");
await AddAttempt(standard.Id, "standard", "active");
var response = await authorized.GetAsync($"/v1/lobbies/{lobbyId}");
Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
Assert.AreEqual(standard.Id, fakeClient.QueriedAgentIds.Single());
```

Also test idle wins, uncertain/reserved are excluded from joining but support metadata-only fallback, same-Agent concurrent calls do not overlap, data failure maps to exact `503 "lobby_data_unavailable"`, and preservation failure persists `WarmupAgent.Status = "quarantined"` plus a stable audit reason.
- [ ] Run `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter LobbyQueryEndpointTests`; expect RED.
- [ ] Implement typed known Agent error handling in `AgentControlClient`; let `LobbyQueryService` map data failures and call `SaveChangesAsync` when quarantining. Register it in `Program.cs`; make the endpoint delegate validation and query result handling to it.
- [ ] Run focused Core test (PASS) and `dotnet build src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj --no-restore` (zero errors/warnings).
- [ ] Commit: `git add src/L4d2MatchmakingCore/Agents/AgentControlClient.cs src/L4d2MatchmakingCore/Lobbies/LobbyQueryService.cs src/L4d2MatchmakingCore/Lobbies/LobbyQueryEndpoints.cs src/L4d2MatchmakingCore/Program.cs src/L4d2MatchmakingCore/tests/LobbyQueryEndpointTests.cs` then `git commit -m "fix(core): 安全调度大厅成员查询"`.

### Task 6: Document and verify the whole contract

**Files:** modify `docs/steam-lobby-agent-api.md`, `docs/matchmaking-core-frontend-api.md`, and `40-atomic-tasks.md`.

**Why / compatibility:** The frontend needs exact status meanings and must compute returned player count as `members.length`, never by subtracting from metadata.

- [ ] Update both API documents with `complete`, each metadata-only status, `503 "lobby_data_unavailable"`, self-filter behavior, and the no-secret boundary. Replace the old claim that no temporary joining occurs.
- [ ] Record every actual RED/GREEN command and exit code in `40-atomic-tasks.md`; omit credentials, tokens, raw Steam logs, and account volume names.
- [ ] Run `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj`, `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj`, and `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj`; expect all PASS.
- [ ] Run the three production builds with `--no-restore`; expect no errors or warnings.
- [ ] Commit: `git add docs/steam-lobby-agent-api.md docs/matchmaking-core-frontend-api.md docs/aegis/work/2026-08-17-lobby-query-membership` then `git commit -m "docs(lobby): 说明可靠成员查询语义"`.

## Live Verification and Rollback

After all tests pass, deploy only to the designated Core host. Use a disposable controlled standard warm-up lobby, query a second controlled lobby, confirm returned members exclude the querying account, and confirm the original operation remains active and still lists its original lobby. Never use a reservation Agent. If preservation fails, leave the Agent quarantined; rollback to the prior image/version only, never to cache-only successful snapshots.
