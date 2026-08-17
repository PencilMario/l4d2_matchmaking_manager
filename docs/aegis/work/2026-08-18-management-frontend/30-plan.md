# Desktop Management Frontend and A2S Observation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `aegis:subagent-driven-development` or `aegis:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Extend Core with a non-persistent A2S observation read model and deliver a desktop-only React/Vite Resource Workspace that displays live server names and player counts with the approved React Bits/Three.js visual system.

**Architecture:** Keep `WarmupSchedulerService` as the owner of fresh A2S decisions. Add a separate five-second `TargetServerObservationCollector` that writes immutable snapshots to an in-memory store; the authenticated `/v1/servers/observations` endpoint joins those snapshots with configured Target Servers and returns `online`, `unavailable`, or `pending` rows. Create a new `frontend/` Vite application that consumes all existing Core routes plus the new read model through a same-origin development proxy.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, MSTest, EF Core, UDP Source A2S; React 19, TypeScript, Vite, Three.js, `postprocessing`, GSAP, Playwright; React Bits components copied from revision `4e0e030193b563be6be33d928f77d0d01cefe237`.

**Baseline / Authority Refs:** `docs/aegis/specs/2026-08-18-management-frontend-design.md`, `docs/matchmaking-core-api.md`, `docs/matchmaking-core-frontend-api.md`, `CONTEXT.md`, `src/L4d2MatchmakingCore/A2s/SourceA2sClient.cs`, `src/L4d2MatchmakingCore/tests/SourceA2sClientTests.cs`.

**Compatibility Boundary:** Preserve every existing `/v1/servers`, `/v1/warmups`, `/v1/agents`, `/v1/lobbies/{lobbyId}` response and failure string. Preserve the scheduler's fresh A2S decisions and reservation/lease behavior. The new Observation route is additive, authenticated, read-only, non-persistent, and never returns RCON, Docker, Steam, or Agent identity data. The frontend supports only desktop viewports of `1280x720` and above.

**Verification:** Run focused MSTest filters for A2S parsing, resolver, collector, and endpoint behavior; run the full Core test project and `dotnet build L4d2MatchmakingManager.sln --no-restore`. Run `npm ci`, `npm run test`, `npm run build`, and Playwright desktop screenshots at `1280x720` and `1440x900`, including a canvas non-blank check and a no-overlap assertion for the data plane.

---

## Scope Decomposition

The Core contract and the frontend are separate implementation owners. Core can be verified with HTTP and unit tests before the frontend exists; the frontend can be verified against a deterministic mock server using the same Observation schema. The final integration task connects the two without adding a second runtime owner.

### Task 1: Extend the canonical A2S INFO parser

**Files:**
- Modify: `src/L4d2MatchmakingCore/A2s/A2sServerInfo.cs`
- Modify: `src/L4d2MatchmakingCore/A2s/SourceA2sClient.cs`
- Test: `src/L4d2MatchmakingCore/tests/SourceA2sClientTests.cs`

**Why this task exists:** Target Server rows need a real A2S server name and `playerCount / maxPlayers`. The existing parser is the only A2S field owner and currently discards the first C-string and maximum-player byte.

**Impact / Compatibility:** Keep `ISourceA2sClient.GetInfoAsync` and challenge handling unchanged. Existing callers continue to read `PlayerCount`; adding fields is source-compatible inside this repository. Truncated packets must still throw `a2s_truncated_info_response` and no previous observation may be reused.

**Verification:** `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~SourceA2sClientTests`.

- [ ] **Step 1: Write the failing parser assertions.** Update the fake INFO response to encode `name`, `map`, `folder`, `game`, AppID, `players=4`, `maxPlayers=8`; assert the returned `ServerName`, `PlayerCount`, `MaxPlayers`, and `ObservedAt`. Add one malformed packet test that ends before `maxPlayers` and expects `a2s_truncated_info_response`.

```csharp
var info = await client.GetInfoAsync(server.Endpoint, CancellationToken.None);

Assert.AreEqual("name", info.ServerName);
Assert.AreEqual(4, info.PlayerCount);
Assert.AreEqual(8, info.MaxPlayers);
Assert.IsTrue(info.ObservedAt > DateTimeOffset.UtcNow.AddSeconds(-2));
```

- [ ] **Step 2: Run the focused test to confirm the baseline is red.** Run the command above. Expected failure: `A2sServerInfo` has no `ServerName`/`MaxPlayers` members and the fake packet still only asserts the old field.

- [ ] **Step 3: Change the record and parser at the existing owner.** Define the record as:

```csharp
public sealed record A2sServerInfo(
    string ServerName,
    int PlayerCount,
    int MaxPlayers,
    DateTimeOffset ObservedAt);
```

Change `ReadCString` to return a decoded string, read the first return value as `serverName`, keep the remaining three reads for map/folder/game validation, skip the two AppID bytes, then read `packet[offset++]` as current players and `packet[offset++]` as maximum players. Preserve the existing header, protocol, challenge, timeout, and truncation checks.

- [ ] **Step 4: Run the focused parser tests to confirm green.** Expected result: all `SourceA2sClientTests` pass, including the challenge count and malformed packet cases.

- [ ] **Step 5: Update all compile-time constructors.** Change `WarmupSchedulerServiceTests.cs` fake `A2sServerInfo` construction to supply a deterministic name and maximum player count; do not change scheduler assertions that only depend on `PlayerCount`.

- [ ] **Step 6: Run the scheduler regression filter.** Run `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupSchedulerServiceTests`. Expected result: existing reservation, target, and A2S failure behavior remains green.

### Task 2: Extract IPv4 resolution and add the observation snapshot owner

**Files:**
- Create: `src/L4d2MatchmakingCore/A2s/A2sEndpointResolver.cs`
- Create: `src/L4d2MatchmakingCore/A2s/TargetServerObservation.cs`
- Create: `src/L4d2MatchmakingCore/A2s/TargetServerObservationStore.cs`
- Create: `src/L4d2MatchmakingCore/A2s/TargetServerObservationCollector.cs`
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2MatchmakingCore/Program.cs`
- Test: `src/L4d2MatchmakingCore/tests/A2sEndpointResolverTests.cs`
- Test: `src/L4d2MatchmakingCore/tests/TargetServerObservationCollectorTests.cs`

**Why this task exists:** The scheduler currently owns two copies of hostname-to-IPv4 resolution. The new collector needs the same rule without creating a third copy. A bounded, in-memory collector gives the browser a stable five-second read model without making each HTTP request emit UDP traffic.

**Impact / Compatibility:** The resolver returns the first IPv4 address exactly as the current scheduler does; no IPv6 support is added. Move the two scheduler DNS blocks to this helper and preserve their existing skip/error branches. The collector never writes PostgreSQL, never creates leases, and never calls Agent APIs.

**Verification:** Run `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "FullyQualifiedName~A2sEndpointResolverTests|FullyQualifiedName~TargetServerObservationCollectorTests"`.

**Repair Track:** The duplicated `Dns.GetHostAddressesAsync` blocks in `WarmupSchedulerService` are the old owner. Replace them with `A2sEndpointResolver.ResolveIpv4Async(host, port, cancellationToken)` so scheduler and collector share one address-selection rule.

**Retirement Track:** Delete the two inline resolver blocks after the focused scheduler tests pass. Keep the scheduler's separate fresh `ISourceA2sClient.GetInfoAsync` call; only address resolution is converged.

- [ ] **Step 1: Write resolver tests.** Cover `localhost` returning an IPv4 `IPEndPoint` and a fake/invalid hostname returning `null` without throwing for a normal DNS failure. Cover cancellation propagating `OperationCanceledException`.

- [ ] **Step 2: Run resolver tests to confirm red.** Expected failure: `A2sEndpointResolver` does not exist.

- [ ] **Step 3: Implement the resolver and refactor scheduler calls.** Use `Dns.GetHostAddressesAsync`, select `AddressFamily.InterNetwork`, and return `new IPEndPoint(address, port)`; return `null` when no IPv4 exists. Replace both scheduler blocks without changing their surrounding `continue`, `return null`, timeout, or logging behavior.

- [ ] **Step 4: Run resolver and scheduler tests.** Run the resolver filter and the existing `WarmupSchedulerServiceTests` filter. Expected result: both pass with unchanged player-target and reservation decisions.

- [ ] **Step 5: Write collector/store tests before implementation.** Use a fake `ISourceA2sClient` keyed by `IPEndPoint` and a two-target input. Assert:
  - success stores `status="online"`, server name, player count, max players, and a current `observedAt`;
  - timeout, DNS failure, and invalid A2S packet store `status="unavailable"` with all live fields `null`;
  - a target with no snapshot is returned as `status="pending"` by the read projection;
  - a later failure replaces the previous online value instead of retaining it.

- [ ] **Step 6: Run collector tests to confirm red.** Expected failure: store, collector, and snapshot types do not exist.

- [ ] **Step 7: Implement the snapshot types and store.** Define `TargetServerObservation` with `TargetServerId`, `Status`, nullable `ServerName`, nullable `PlayerCount`, nullable `MaxPlayers`, and nullable `ObservedAt`. Use a singleton `TargetServerObservationStore` backed by an immutable replacement dictionary; expose `Get(Guid)` and `Replace(Guid, TargetServerObservation)` without returning mutable collections.

- [ ] **Step 8: Implement one refresh cycle and the hosted loop.** `TargetServerObservationCollector.RefreshOnceAsync` loads all configured Target Servers through a scope, resolves each endpoint, queries the shared A2S client with the host cancellation token, and writes either an online snapshot or an unavailable snapshot. Execute targets with bounded `Parallel.ForEachAsync` (maximum 16 concurrent targets). `ExecuteAsync` performs one immediate refresh, then waits five seconds between non-overlapping cycles; cancellation exits without writing a fabricated failure.

- [ ] **Step 9: Register the singleton store and non-Testing hosted collector.** In `Program.cs`, add the store before `AddHostedService<TargetServerObservationCollector>()`; keep the existing Testing guard so unit endpoint tests remain deterministic and the scheduler's Testing behavior is unchanged.

- [ ] **Step 10: Run collector, scheduler, and full Core tests.** Run the two focused filters followed by `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj`. Expected result: no existing scheduling or authentication regression.

### Task 3: Expose the authenticated Observation read model

**Files:**
- Create: `src/L4d2MatchmakingCore/Servers/TargetServerObservationDtos.cs`
- Create: `src/L4d2MatchmakingCore/Servers/TargetServerObservationService.cs`
- Create: `src/L4d2MatchmakingCore/Servers/TargetServerObservationEndpoints.cs`
- Modify: `src/L4d2MatchmakingCore/Program.cs`
- Test: `src/L4d2MatchmakingCore/tests/TargetServerObservationEndpointTests.cs`
- Modify: `docs/matchmaking-core-api.md`
- Modify: `docs/matchmaking-core-frontend-api.md`

**Why this task exists:** The frontend needs one bulk read that includes every configured Target Server without triggering A2S itself. The route must distinguish configured resources from the current runtime sample.

**Impact / Compatibility:** Add only `GET /v1/servers/observations`; keep existing `/v1/servers` payloads unchanged. The route requires the existing Bearer handler and returns `200 []` when no Target Servers exist. It returns one row per configured Target Server, with `pending` when the store has no row.

- [ ] **Step 1: Write endpoint tests.** Add tests for missing/wrong Bearer token (`401`), empty configured list (`200 []`), configured target with no snapshot (`pending` and null live fields), online snapshot serialization, unavailable snapshot nulling, and literal route precedence against `GET /v1/servers/{serverId:guid}`.

- [ ] **Step 2: Run the endpoint filter to confirm red.** Run `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~TargetServerObservationEndpointTests`. Expected failure: route and response types do not exist.

- [ ] **Step 3: Implement the response and read projection.** Use:

```csharp
public sealed record TargetServerObservationResponse(
    Guid TargetServerId,
    string Status,
    string? ServerName,
    int? PlayerCount,
    int? MaxPlayers,
    DateTimeOffset? ObservedAt);
```

The service loads Target Server IDs from `MatchmakingDbContext.AsNoTracking()`, joins each ID to `TargetServerObservationStore.Get`, and projects `pending` when absent. It must never issue `ISourceA2sClient` calls.

- [ ] **Step 4: Map the literal route before the UUID route.** Add `group.MapGet("/observations", ListObservationsAsync)` before `group.MapGet("/{serverId:guid}", GetAsync)` in `TargetServerEndpoints`, or register a dedicated endpoint method before the existing group mapping. Require authorization and return `TypedResults.Ok` with the array.

- [ ] **Step 5: Run focused endpoint tests and Core authentication tests.** Expected result: the new route passes and existing `/v1/servers` authentication/payload tests remain green.

- [ ] **Step 6: Update both API references.** Add the route to the route lists, document the schema and `online/unavailable/pending` semantics, state that failures never reuse old live fields, and add the five-second browser polling guidance. Keep existing sample response language and all sensitive-field exclusions.

- [ ] **Step 7: Run documentation and Core verification.** Run `git diff --check`, the endpoint filter, `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj`, and `dotnet build L4d2MatchmakingManager.sln --no-restore`.

### Task 4: Create the desktop React/Vite application shell

**Files:**
- Create: `frontend/package.json`
- Create: `frontend/package-lock.json`
- Create: `frontend/index.html`
- Create: `frontend/tsconfig.json`
- Create: `frontend/tsconfig.node.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/vitest.config.ts`
- Create: `frontend/playwright.config.ts`
- Create: `frontend/src/main.tsx`
- Create: `frontend/src/App.tsx`
- Create: `frontend/src/styles/tokens.css`
- Create: `frontend/src/styles/global.css`
- Create: `frontend/src/components/react-bits/README.md`
- Create: `frontend/README.md`

**Why this task exists:** There is no frontend package in the repository. This creates a repeatable desktop development surface without changing the .NET solution or exposing Core publicly.

**Impact / Compatibility:** `frontend/` is an independent Node application. Vite proxies `/healthz` and `/v1` to `CORE_ORIGIN` (default `http://127.0.0.1:18080`) during development; production still requires same-origin reverse proxying. No token is placed in the proxy URL.

**Verification:** `npm --prefix frontend ci`, `npm --prefix frontend run build`, and `npm --prefix frontend run test -- --run`.

- [ ] **Step 1: Create the package manifest and install exact dependencies.** Define scripts `dev`, `build`, `test`, `test:watch`, and `test:e2e`. Install React 19, React DOM, TypeScript, Vite, `@vitejs/plugin-react`, `three`, `@types/three`, `postprocessing`, `gsap`, `lucide-react`, Vitest, Testing Library, jsdom, and Playwright. Run `npm install` from `frontend/` to generate the lockfile.

- [ ] **Step 2: Configure Vite and test runners.** `vite.config.ts` must proxy `/healthz` and `/v1` to `process.env.CORE_ORIGIN ?? "http://127.0.0.1:18080"`. Vitest uses `jsdom` and the React plugin. Playwright uses Chromium only, starts Vite on a fixed local port, and defines projects for `1280x720` and `1440x900`.

- [ ] **Step 3: Add the application entry and desktop tokens.** `main.tsx` imports `tokens.css`, `global.css`, and mounts `App`. Set a desktop minimum width of `1280px`, a fixed full-viewport stage, signal colors for online/active/attention/failure, and z-index layers for WebGL background, pointer effects, data plane, dialogs, and toasts. Do not add mobile breakpoints.

- [ ] **Step 4: Copy React Bits source with provenance.** Copy the JS/TS-compatible source and CSS for `Hyperspeed`, `Cubes`, `TargetCursor`, `AnimatedList`, `Counter`, `Stepper`, `ClickSpark`, `GlassSurface`, `SpotlightCard`, and `LineSidebar` into `frontend/src/components/react-bits/`. Record the upstream URL, revision, component paths, and MIT + Commons Clause notice in `README.md`; do not fetch components at runtime.

- [ ] **Step 5: Verify the shell before feature work.** Run `npm run build` and `npm run test`. Expected result: Vite produces `frontend/dist` and Vitest exits with zero tests or the explicit shell smoke test, without a TypeScript error.

### Task 5: Implement the typed Core client and five-second frontend snapshot

**Files:**
- Create: `frontend/src/api/models.ts`
- Create: `frontend/src/api/core-client.ts`
- Create: `frontend/src/state/useCoreSnapshot.ts`
- Create: `frontend/src/state/core-errors.ts`
- Test: `frontend/src/api/core-client.test.ts`
- Test: `frontend/src/state/useCoreSnapshot.test.tsx`

**Why this task exists:** All pages depend on consistent authentication, polling, and stale/error semantics. Centralizing them prevents each view from inventing a different interpretation of `401`, `409`, `500`, `503`, `pending`, or `metadata-only`.

**Impact / Compatibility:** The client sends `Authorization: Bearer <token>` only in memory. It parses direct object/array responses and JSON-string error bodies; it never wraps responses in `{ data: ... }` and never stores `rconPassword` from reads.

**Verification:** `npm --prefix frontend run test -- --run src/api/core-client.test.ts src/state/useCoreSnapshot.test.tsx`.

- [ ] **Step 1: Define models exactly from the two API docs plus Observation.** Include `TargetServer`, `WarmupAgent`, `WarmupStatus`, `LobbySnapshot`, `TargetServerObservation`, and the `CoreApiError` shape. Make live Observation fields nullable and keep `phase` as `string`.

- [ ] **Step 2: Write client tests before implementation.** Mock `fetch` and assert the token header, direct array parsing, JSON-string error parsing, `204` handling, and that a `409` preserves its error code. Assert `getServerObservations()` never calls `/v1/servers/{id}` in a loop.

- [ ] **Step 3: Implement `core-client.ts`.** Provide `getHealth`, `listServers`, `listServerObservations`, `listAgents`, `listWarmups`, `queryLobby`, and the existing server/agent write/action methods. Normalize non-2xx responses to `{ status, code, retryable }` while retaining raw safe error strings; do not include token or passwords in thrown messages.

- [ ] **Step 4: Implement `useCoreSnapshot`.** On initial load fetch servers, agents, warmups, and observations in parallel. Every five seconds refresh warmups, agents, and observations through one abortable cycle. Pause a cycle while a write form is dirty; after a successful write refresh all affected resources. Keep the last successful snapshot marked stale when a read fails.

- [ ] **Step 5: Add the `401` session transition.** Clear the in-memory token and show the token configuration view; do not retry the same unauthorized request.

- [ ] **Step 6: Run API/state tests.** Run `npm --prefix frontend run test -- --run src/api/core-client.test.ts src/state/useCoreSnapshot.test.tsx`. Expected result: all request, polling, abort, stale, and error mapping assertions pass.

### Task 6: Build the immersive Resource Workspace shell

**Files:**
- Create: `frontend/src/features/workspace/WorkspaceShell.tsx`
- Create: `frontend/src/features/workspace/WorkspaceNav.tsx`
- Create: `frontend/src/features/workspace/CoreStatusBar.tsx`
- Create: `frontend/src/features/workspace/VisualStage.tsx`
- Create: `frontend/src/features/workspace/WorkspaceShell.test.tsx`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/styles/global.css`

**Why this task exists:** The approved design needs the Resource Workspace and the full visual layer before resource views are added.

**Impact / Compatibility:** The WebGL layer is decorative and `pointer-events: none`; no effect may cover the data plane, dialogs, or keyboard focus. `Hyperspeed` is mounted once per application session, not on each five-second snapshot.

**Verification:** `npm --prefix frontend run test -- --run src/features/workspace/WorkspaceShell.test.tsx` and `npm --prefix frontend run build`.

- [ ] **Step 1: Write shell tests.** Assert the Core status bar, four navigation labels, active view state, desktop minimum width, and that the visual stage has a canvas/background layer behind the data layer.

- [ ] **Step 2: Implement `VisualStage`.** Mount `Hyperspeed` full-bleed with the approved colors, `Cubes` as the Target Server topology band, and `TargetCursor` over the desktop interaction layer. Set `aria-hidden="true"` on decorative canvas nodes and keep the data plane in a higher stacking context.

- [ ] **Step 3: Implement the shell and navigation.** Render `LineSidebar`, `CoreStatusBar`, active tab content, global refresh, and a session token entry view. Use Lucide icons for commands and tooltips for icon-only refresh/close actions.

- [ ] **Step 4: Run shell tests and build.** Run the shell test file and `npm --prefix frontend run build`. Expected result: no canvas mount error in jsdom tests and a successful desktop bundle.

### Task 7: Implement Warm-up and Target Server views

**Files:**
- Create: `frontend/src/features/warmups/WarmupWorkspace.tsx`
- Create: `frontend/src/features/servers/TargetServerTable.tsx`
- Create: `frontend/src/features/servers/TargetServerRow.tsx`
- Create: `frontend/src/features/servers/TargetServerDrawer.tsx`
- Create: `frontend/src/features/servers/TargetServerForm.tsx`
- Create: `frontend/src/features/servers/server-view-model.ts`
- Test: `frontend/src/features/warmups/WarmupWorkspace.test.tsx`
- Test: `frontend/src/features/servers/TargetServerTable.test.tsx`

**Why this task exists:** This is the main user journey: see the A2S server name and live population while comparing configuration and active warmups.

**Impact / Compatibility:** Join configuration and Observation by `targetServerId`; never use `metadata["Members:numPlayers"]` or Warm-up Agent identity as a substitute. Preserve row height and column tracks while values refresh.

**Verification:** `npm --prefix frontend run test -- --run src/features/warmups/WarmupWorkspace.test.tsx src/features/servers/TargetServerTable.test.tsx`.

- [ ] **Step 1: Write view tests for online, unavailable, and pending rows.** Assert online rows render `serverName`, `endpoint`, `playerCount / maxPlayers`, and relative `observedAt`; unavailable rows render endpoint plus “A2S 不可用” and `-- / --`; pending rows render “等待首次观测”. Assert disabled configuration remains actionable when A2S is unavailable.

- [ ] **Step 2: Implement the Warm-up workspace.** Render `SpotlightCard` summaries, `AnimatedList` rows, stable phase text, `uncertain` warning, `remainingSeconds`, and the `Cubes` selection filter. Do not add controls that create lobbies or restart quarantined Agents.

- [ ] **Step 3: Implement the Target Server table and drawer.** Use A2S server name as primary text, endpoint as secondary monospace text, `Counter` for the player pair, and explicit status/observed time. Keep configuration fields and operation controls separate from live fields.

- [ ] **Step 4: Implement the complete PUT form.** Initialize all fields from the current response; show `rconPassword` only when `requiresReservation` is true; submit the full body with `null` for cleared optional values; never merge or rehydrate a password from a read response.

- [ ] **Step 5: Implement drain/delete confirmation.** Use `Stepper` for the warning and final command. On `409 target_server_drain_failed`, keep the form and row, refresh servers/warmups/observations, and expose a retry with `enabled=false`; on success, refresh instead of optimistic removal.

- [ ] **Step 6: Run warmup/server tests.** Run both focused test files and the full frontend unit suite. Expected result: stable rows, correct A2S states, complete PUT body, and drain semantics are green.

### Task 8: Implement Warm-up Agent and Lobby Query views

**Files:**
- Create: `frontend/src/features/agents/AgentTable.tsx`
- Create: `frontend/src/features/agents/AgentForm.tsx`
- Create: `frontend/src/features/agents/AgentActions.tsx`
- Create: `frontend/src/features/lobbies/LobbyQueryView.tsx`
- Create: `frontend/src/features/lobbies/LobbyMetadataTable.tsx`
- Test: `frontend/src/features/agents/AgentTable.test.tsx`
- Test: `frontend/src/features/lobbies/LobbyQueryView.test.tsx`

**Why this task exists:** The approved workspace includes the existing Agent lifecycle and explicit Lobby diagnostics; these views must not silently weaken the Core safety contract.

**Impact / Compatibility:** `quarantined` has no automatic restart/reuse command. `recreate` and delete require confirmation. Lobby members render only for `memberDataStatus=complete`; metadata-only responses keep an empty member list and a visible explanation.

**Verification:** `npm --prefix frontend run test -- --run src/features/agents/AgentTable.test.tsx src/features/lobbies/LobbyQueryView.test.tsx`.

- [ ] **Step 1: Write Agent tests.** Assert status-dependent action visibility, `quarantined` no-auto-restart, create/update validation, `recreate` confirmation, and full-list refresh after delete.

- [ ] **Step 2: Implement Agent table/form/actions.** Use `AnimatedList` for stable row entry, `Stepper` for recreate/delete, preserve `downloadRegion` null/blank semantics, and show noVNC as a loopback port rather than a public URL.

- [ ] **Step 3: Write Lobby tests.** Assert decimal non-zero validation before request, complete member rendering, each metadata-only status, and safe presentation of the three 503 error strings.

- [ ] **Step 4: Implement Lobby query.** Send only explicit submissions; render metadata as a string dictionary without assuming a fixed key set; mark an old successful result stale after a later 503 rather than silently clearing it or claiming a fresh member count.

- [ ] **Step 5: Run Agent/Lobby tests.** Run the two focused test files and the full frontend unit suite. Expected result: no API calls occur for hidden actions or automatic lobby polling.

### Task 9: Add desktop visual and journey verification

**Files:**
- Create: `frontend/e2e/resource-workspace.spec.ts`
- Create: `frontend/e2e/fixtures/core-mock.ts`
- Modify: `frontend/playwright.config.ts`
- Create: `frontend/e2e/screenshots/.gitkeep`

**Why this task exists:** Heavy WebGL and pointer effects can render blank or cover the operational data even while unit tests pass. The main desktop journey must be verified against deterministic Core responses.

**Impact / Compatibility:** Tests use a mock same-origin `/healthz`, `/v1/servers`, `/v1/servers/observations`, `/v1/agents`, `/v1/warmups`, and `/v1/lobbies/{id}` server; they do not send real Steam or Core credentials. The tests must not make the implementation mobile-responsive.

**Verification:** `npm --prefix frontend run test:e2e` with Chromium projects at `1280x720` and `1440x900`.

- [ ] **Step 1: Build the deterministic mock fixture.** Return one online server (`L4D2 HK Versus #1`, `2/12`), one unavailable server, one pending server, one active warmup, one quarantined Agent, and a complete Lobby response. Include handlers for a drain `409` and a Lobby `503`.

- [ ] **Step 2: Write desktop journey assertions.** At `1280x720` and `1440x900`, open the workspace, assert the server name/endpoint/player count, click a topology unit to filter rows, open the edit drawer, trigger drain `409`, and confirm the form/row remain. Query a Lobby and assert metadata-only/member-complete states.

- [ ] **Step 3: Add canvas and overlap checks.** Assert a non-zero canvas pixel sample for `Hyperspeed` or the deterministic visual fallback, inspect bounding boxes for server name/player/status/form controls, and fail if the visual stage intersects the data plane or if text overflows its row.

- [ ] **Step 4: Run Playwright.** Run `npm --prefix frontend run test:e2e`. Expected result: both desktop projects pass; screenshots are generated only when explicitly requested and are not committed as default output.

### Task 10: Documentation, local run path, and final regression

**Files:**
- Modify: `docs/matchmaking-core-api.md`
- Modify: `docs/matchmaking-core-frontend-api.md`
- Create: `frontend/README.md`
- Modify: `docs/aegis/INDEX.md`

**Why this task exists:** The new route, desktop-only support, local proxy, React Bits provenance, and verification commands must be discoverable by the next engineer.

**Impact / Compatibility:** Documentation must state that frontend calls are same-origin/BFF mediated, Core/Agent ports remain loopback/private, and the Observation route is display-only. It must not imply that the route changes scheduling truth or exposes server credentials.

**Verification:** `git diff --check`, link-target checks for all new Markdown references, the full Core test/build bundle, frontend unit/build bundle, and Playwright desktop projects.

- [ ] **Step 1: Update API docs.** Document the route, response model, statuses, null-field rule, five-second polling, auth, and route ordering. Keep existing endpoint examples and error bodies unchanged.

- [ ] **Step 2: Write `frontend/README.md`.** Include `npm ci`, `npm run dev`, `CORE_ORIGIN`, `npm run test`, `npm run test:e2e`, desktop viewport support, and the React Bits pinned revision/license note.

- [ ] **Step 3: Update the Aegis index.** Link the approved spec, this plan, and the atomic task checklist without removing existing work records.

- [ ] **Step 4: Run the final verification bundle.** Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj
dotnet build L4d2MatchmakingManager.sln --no-restore
npm --prefix frontend ci
npm --prefix frontend run test -- --run
npm --prefix frontend run build
npm --prefix frontend run test:e2e
git diff --check
```

Expected result: all Core tests, frontend unit tests, desktop Playwright projects, build, and whitespace checks exit successfully. A real target-server/A2S deployment remains a separate manual acceptance because the repository does not provide production Steam credentials or a guaranteed live server.

## Compatibility and Retirement Summary

- **Stays:** `WarmupSchedulerService` remains the runtime decision owner; all existing public routes, payloads, auth, failure strings, RCON secrecy, Agent isolation, and reservation leases remain unchanged.
- **Shrinks:** inline scheduler DNS/IPv4 resolution shrinks to the shared `A2sEndpointResolver`.
- **New owner:** `TargetServerObservationCollector` owns only non-persistent UI snapshots; `TargetServerObservationService` owns only the public projection.
- **Retires:** parser-side discarded A2S server-name/max-player fields and duplicated scheduler DNS blocks; no old-value fallback is retained.

## TodoCheckpointDraft

- Current todo: update the approved spec for desktop-only scope, write this implementation plan and atomic checklist.
- Completed: API/design exploration, React Bits revision inspection, A2S/parser baseline read, user approval of visual design and desktop-only boundary.
- Active slice: plan authoring; no source implementation started.
- Evidence refs: `6a91a71` design commit, current `SourceA2sClient`/test files, Core route registration.
- Blocked on: nothing for planning; implementation requires user selection of subagent-driven or inline execution after this plan review.
- Next: review plan, then execute Task 1 in an isolated implementation worktree.

## DriftCheckDraft

- Scope: desktop frontend plus additive Core Observation contract; mobile explicitly excluded.
- Compatibility: scheduler, existing API shapes, auth, secrets and Agent boundaries remain owners.
- Retirement: duplicated DNS blocks and discarded parser fields have explicit removal points.
- Decision: `pause-for-user` after plan handoff; no implementation claim is made by this plan.
