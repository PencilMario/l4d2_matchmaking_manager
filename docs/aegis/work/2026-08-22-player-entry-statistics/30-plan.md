# 玩家进入统计实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 记录有效 `ReplyJoinData` 成功响应，并提供按 Agent、上海时区时间趋势、显式下载区域和目标模式筛选的 180 天内统计页面。

**Architecture:** `SteamNativeRuntime` 在成功发送回复后只做一次非阻塞事件入队；Agent 后台 uploader 批量向 Core 专用端点上报。Core 将带快照的原始事件按 `EventId` 幂等写入 PostgreSQL，再在数据库侧完成过滤、聚合和上海时区零桶补齐；现用管理前端通过只读 API 展示筛选结果。

**Tech Stack:** .NET 8 Steam probe, .NET 10 Agent/Core, shared C# contracts, ASP.NET Minimal APIs, EF Core 10/Npgsql, PostgreSQL, React/TypeScript/Vite, existing CSS/SVG/HTML.

**Baseline / Authority Refs:** `CONTEXT.md`; `docs/aegis/specs/2026-08-23-player-entry-statistics-design.md`; `docs/aegis/work/2026-08-22-player-entry-statistics/00-intent.md`; `10-baseline-readset.md`; `20-impact.md`; `src/L4d2Matchmaking.Contracts/AgentContracts.cs`; `research/SteamLobbyProbe/ActiveLobbyJoinDataResponder.cs`; `research/SteamLobbyProbe/SteamNativeRuntime.cs`; `src/L4d2MatchmakingCore/Data/Entities.cs`; `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`; `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`; `src/L4d2MatchmakingCore/Agents/WarmupAgentContainerService.cs`; `src/L4d2MatchmakingCore/Program.cs`; `frontend/src/App.tsx`; `frontend/src/components/Sidebar.tsx`; `frontend/src/services/api.ts`.

**Compatibility Boundary:** Existing operation JSON and Steam protocol behavior remain valid; the new operation context is optional. Existing management bearer authentication remains restricted to management endpoints; Agent reporting uses a separate per-Agent token. Missing reporting variables disable only uploader statistics. Statistics never stores Steam IDs, never uses runtime-probed download regions, never rewrites historical snapshots, and never blocks Steam callbacks, reply sending, scheduling, or lobby lifecycle.

**Verification:** Run focused MSTest suites after each red-green slice, then all .NET tests, frontend tests/build, migration/model checks, and a final diff review. PostgreSQL-specific tests use the existing Testcontainers pattern when Docker is available; if unavailable, retain deterministic service/unit coverage and report the blocked integration command with manual HTTP/SQL checks.

---

### Task 1: Establish test seams and shared event contract

**Files:**
- Modify: `src/L4d2Matchmaking.Contracts/AgentContracts.cs`
- Create: `src/L4d2Matchmaking.Contracts/PlayerEntryStatisticsContracts.cs`
- Test: `src/L4d2Matchmaking.Contracts/tests/PlayerEntryStatisticsContractsTests.cs`
- Create: `docs/aegis/work/2026-08-22-player-entry-statistics/50-evidence.md`

**Why this task exists:** Every producer and consumer must agree on the event snapshot, lobby/mode values, optional operation context, and batch response. The contract must preserve old requests that omit the new context.

**Impact / Compatibility:** Add optional `EntryStatisticsContext` to `AgentOperationRequest`; do not change positional constructor meaning. Define `PlayerEntryEvent`, `PlayerEntryEventBatchRequest`, `PlayerEntryEventBatchResponse`, `EntryStatisticsContext`, and stable normalization constants. Do not expose Steam IDs.

**Verification:** `dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj --filter PlayerEntryStatisticsContractsTests`.

- [ ] **Step 1: Write the failing tests** for old request JSON round-trip, event fields, empty mode normalization to `versus`, empty region as default, and batch max-size constant.
- [ ] **Step 2: Run the focused test** and confirm it fails because the new types/context are absent.
- [ ] **Step 3: Add the minimal records/constants** and the optional init-only context property.
- [ ] **Step 4: Run focused and existing contract tests** and confirm both pass.
- [ ] **Step 5: Record the exact command/output and compatibility evidence** in `50-evidence.md`.

### Task 2: Capture only successful ReplyJoinData sends

**Files:**
- Modify: `research/SteamLobbyProbe/ActiveLobbyJoinDataResponder.cs`
- Modify: `research/SteamLobbyProbe/SteamNativeRuntime.cs`
- Modify: `research/SteamLobbyProbe/SteamSessionActor.cs`
- Modify: `research/SteamLobbyProbe/SteamSessionContracts.cs`
- Test: `research/SteamLobbyProbe/tests/ActiveLobbyJoinDataResponderTests.cs`
- Create: `research/SteamLobbyProbe/tests/PlayerEntryEventCaptureTests.cs`
- Modify: `src/L4d2LobbyAgent/Steam/AgentSteamSessionService.cs`

**Why this task exists:** The metric is an effective request/reply response count, not a lobby-member count. An event must be emitted after `SendLobbyChatMsg` returns true and never for invalid requests, failed encoding, failed sends, inactive lobbies, or callback exceptions.

**Impact / Compatibility:** Inject an optional `IPlayerEntryEventSink` into the runtime/actor path. Keep the Steam callback synchronous and bounded to a non-blocking `TryEnqueue`; catch/log sink failures without altering the send result. Pass the current operation context and lobby snapshot into the runtime; old operations without context produce no incomplete event.

**Verification:** Focused probe tests assert two successful sends create two events, failed paths create none, and callback completion does not await network work; existing protocol and actor tests remain green.

- [ ] **Step 1: Add capture/sink test doubles and failing tests** around the successful-send branch.
- [ ] **Step 2: Run `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter PlayerEntryEventCaptureTests`** and confirm the event count assertions fail.
- [ ] **Step 3: Thread optional operation context and sink through actor/runtime**, create an event after a successful send, and keep the existing log without Steam recipient details.
- [ ] **Step 4: Run probe focused tests plus `SteamLobbyProbe.Tests.csproj`** and fix only production regressions caused by the new seam.

### Task 3: Build bounded Agent queue and uploader

**Files:**
- Create: `src/L4d2LobbyAgent/Reporting/PlayerEntryReportingOptions.cs`
- Create: `src/L4d2LobbyAgent/Reporting/PlayerEntryEventQueue.cs`
- Create: `src/L4d2LobbyAgent/Reporting/PlayerEntryEventUploader.cs`
- Create: `src/L4d2LobbyAgent/Reporting/PlayerEntryEventSink.cs`
- Modify: `src/L4d2LobbyAgent/Program.cs`
- Modify: `src/L4d2LobbyAgent/Steam/AgentSessionOptions.cs`
- Test: `src/L4d2LobbyAgent/tests/PlayerEntryReportingTests.cs`

**Why this task exists:** Reporting failures are allowed, but Steam callbacks and ReplyJoinData must remain non-blocking. A 4096-item bounded channel, batches of 100, two-second timeout, and one transport/5xx retry establish the approved failure boundary.

**Impact / Compatibility:** Missing Agent ID/origin/token disables the uploader and sink without affecting existing endpoints. Shutdown cancels the uploader and does not drain synchronously. Queue-full, retry, discard, and successful batch counters are logged without bearer tokens or raw payloads.

**Verification:** Queue tests prove `TryEnqueue` returns immediately on full capacity; uploader tests prove 202 success, one retry for transport/5xx, no retry for 4xx, and release on failure. Existing Agent endpoint tests must still pass with no reporting environment.

- [ ] **Step 1: Write failing queue/uploader tests** using a fake `HttpMessageHandler` and cancellation token.
- [ ] **Step 2: Run the focused Agent tests** and verify missing implementation failures.
- [ ] **Step 3: Implement options, bounded queue, sink, uploader and DI registration** with no blocking callback call.
- [ ] **Step 4: Run focused and full Agent tests**.
- [ ] **Step 5: Add the Agent reporting environment names to deployment/API docs** after the implementation shape is fixed.

### Task 4: Add Core event storage and migration

**Files:**
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`
- Modify: `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`
- Create: `src/L4d2MatchmakingCore/Data/Migrations/202608230001_PlayerEntryEvents.cs`
- Test: `src/L4d2MatchmakingCore/tests/PlayerEntryEventModelTests.cs`
- Modify: `src/L4d2MatchmakingCore/tests/MigrationDiscoveryTests.cs`

**Why this task exists:** Raw events are the source of truth for 180 days and must survive Agent/Target Server rename or deletion through snapshots, without foreign keys or cascading deletes.

**Impact / Compatibility:** Add `PlayerEntryEvent` with UUID primary key, UTC event/ingest times, operation/lobby fields, Agent/Target Server IDs and snapshots, nullable configured region, and normalized coop/versus mode. Add event-time and dimension-time indexes; migration only creates the table/indexes and does not backfill.

**Verification:** EF model tests assert required lengths/indexes/no relationships; migration discovery asserts the new migration and model snapshot metadata. PostgreSQL migration smoke test runs if Docker is available.

- [ ] **Step 1: Write failing model/migration tests** for entity mapping, key and required indexes.
- [ ] **Step 2: Run focused tests** and verify the new mapping/migration is missing.
- [ ] **Step 3: Add the entity, model mapping and migration** using the repository's existing migration style.
- [ ] **Step 4: Run model/migration tests and inspect generated SQL/schema expectations**.

### Task 5: Add restricted Agent reporting authentication and ingestion

**Files:**
- Create: `src/L4d2MatchmakingCore/Auth/AgentReportingAuthenticationHandler.cs`
- Create: `src/L4d2MatchmakingCore/Statistics/PlayerEntryEventIngestionService.cs`
- Create: `src/L4d2MatchmakingCore/Statistics/PlayerEntryEventEndpoints.cs`
- Modify: `src/L4d2MatchmakingCore/Configuration/CoreOptions.cs`
- Modify: `src/L4d2MatchmakingCore/Program.cs`
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`
- Modify: `src/L4d2MatchmakingCore/Agents/WarmupAgentContainerService.cs`
- Modify: `src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs`
- Modify: `src/L4d2MatchmakingCore/Agents/IAgentContainerRuntime.cs`
- Modify: `src/L4d2MatchmakingCore/Agents/DockerAgentContainerRuntime.cs`
- Test: `src/L4d2MatchmakingCore/tests/PlayerEntryEventIngestionTests.cs`
- Test: `src/L4d2MatchmakingCore/tests/AgentReportingAuthenticationTests.cs`

**Why this task exists:** Agent upload must be limited to its own events and must not receive management privileges. Event ID idempotency makes one retry safe; conflicting content for one ID must be rejected.

**Impact / Compatibility:** Store only SHA-256 token hashes on `WarmupAgent`; inject a fresh token into create/recreate container definitions only after the new container succeeds. Existing agents without a token continue running but do not report until recreated. Management `CoreBearer` remains unchanged.

**Verification:** API tests cover missing/management/wrong Agent token (401), valid token (202), cross-Agent rejection, duplicate count without duplicate row, conflict 409, invalid fields/modes/time/batch 400, and no secret in response/logging. Container-definition tests assert origin/token environment injection and no token in API response.

- [ ] **Step 1: Write failing ingestion/auth/container tests** with in-memory EF for validation and fake runtime for environment assertions.
- [ ] **Step 2: Run focused Core tests** and verify red failures.
- [ ] **Step 3: Implement token generation/hash verification, ingestion validation/idempotency, endpoint and safe create/recreate injection.**
- [ ] **Step 4: Run focused tests and existing Core endpoint/service tests.**

### Task 6: Populate operation snapshots from scheduling

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2MatchmakingCore/Agents/AgentControlClient.cs`
- Test: `src/L4d2MatchmakingCore/tests/AgentControlClientTests.cs`
- Test: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** Events need immutable Agent, configured region, Target Server address/name, IDs, and effective mode snapshots from operation creation rather than current configuration at query time.

**Impact / Compatibility:** Fill the optional context when Core starts a warmup. Normalize a missing Target Server `GameMode` to `versus`; preserve the existing `GameMode` request property and all old operation behavior. The operation request remains JSON-compatible with older Agents.

**Verification:** Scheduler/client tests assert exact context JSON for standard/reserved, explicit/null region, target name best effort, and null mode -> versus; old request serialization still omits optional context when absent.

- [ ] **Step 1: Add failing scheduler/client assertions** for context fields and effective mode.
- [ ] **Step 2: Run focused tests and verify the context is absent.**
- [ ] **Step 3: Build the context from the selected plan and pass it to AgentControlClient** without changing selection/lifecycle decisions.
- [ ] **Step 4: Run the scheduler/client regression tests.**

### Task 7: Implement Shanghai-time statistical query and retention cleanup

**Files:**
- Create: `src/L4d2MatchmakingCore/Statistics/PlayerEntryStatisticsQuery.cs`
- Create: `src/L4d2MatchmakingCore/Statistics/PlayerEntryStatisticsEndpoints.cs`
- Create: `src/L4d2MatchmakingCore/Statistics/PlayerEntryRetentionService.cs`
- Create: `src/L4d2MatchmakingCore/Statistics/PlayerEntryStatisticsModels.cs`
- Modify: `src/L4d2MatchmakingCore/Program.cs`
- Test: `src/L4d2MatchmakingCore/tests/PlayerEntryStatisticsQueryTests.cs`
- Test: `src/L4d2MatchmakingCore/tests/PlayerEntryRetentionServiceTests.cs`

**Why this task exists:** Operators need total responses, actual-hours frequency, configurable trend buckets, fixed Shanghai 0–23 pattern, lobby/mode/server/Agent filters, current zero rows, and historical snapshots inside the same 180-day raw window.

**Impact / Compatibility:** `from` defaults to now-24h, `to` to now; `[from,to)` rejects empty, >180-day, >5-minute future, invalid enum and >5000-bucket ranges. `auto` chooses hour/day/week as designed. PostgreSQL performs filtering/aggregation; service fills zero buckets and computes all frequencies using actual selected duration. Current Agent/explicit region rows are joined only for display and do not rewrite event facts.

**Verification:** Query tests cover recent/default ranges, Shanghai cross-midnight and Monday week boundaries, zero buckets, 24 daily buckets, modes including null->versus, lobby types, dimension filters, idle-hour denominators, renamed/deleted snapshots and >5000 validation. Retention tests delete only events older than 180 days and swallow/log failures.

- [ ] **Step 1: Write failing query/retention tests** with deterministic clock and seeded event rows.
- [ ] **Step 2: Run focused tests and confirm the aggregation service is absent.
- [ ] **Step 3: Implement normalization, PostgreSQL-compatible grouped queries, Shanghai bucket filling, DTOs/endpoints and hourly cleanup hosted service.
- [ ] **Step 4: Run focused tests, Core API tests and model tests.**

### Task 8: Add management frontend statistics journey

**Files:**
- Modify: `frontend/src/types/index.ts`
- Modify: `frontend/src/services/api.ts`
- Modify: `frontend/src/components/Sidebar.tsx`
- Modify: `frontend/src/App.tsx`
- Create: `frontend/src/components/PlayerEntryStatisticsView.tsx`
- Create: `frontend/src/components/PlayerEntryStatisticsView.test.tsx`
- Create: `frontend/src/components/player-entry-statistics.css`

**Why this task exists:** The main user journey is entering the statistics page, choosing quick/custom Shanghai time, filtering lobby/mode/server/Agent, querying once, and reading trend, daily pattern, Agent and region results.

**Impact / Compatibility:** Add a single `statistics` tab to the mounted `App`/Sidebar path; do not wire the unused workspace shell or add polling. Use existing CSS/SVG/HTML, no chart dependency. Custom date-time inputs must parse as Shanghai local time explicitly, and labels must explain `versus` includes unspecified mode and `默认` means no explicit Agent region.

**Verification:** `npm run test -- --run` with component/API assertions and `npm run build`; manual/E2E check at 1280x720 covers initial load, filters, loading, empty, error/retry and readable SVG/data text.

- [ ] **Step 1: Write failing API/component tests** for query serialization, Shanghai conversion, filter labels and all response sections.
- [ ] **Step 2: Run the focused frontend test and verify the new view/API method is absent.
- [ ] **Step 3: Implement typed API query serialization and the statistics view with accessible SVG/data tables and visible states.
- [ ] **Step 4: Add navigation/rendering and run focused frontend tests.
- [ ] **Step 5: Run the complete frontend tests and production build.**

### Task 9: Document deployment and operational boundaries

**Files:**
- Modify: `docs/matchmaking-core-api.md`
- Modify: `docs/matchmaking-core-frontend-api.md`
- Modify: `docs/steam-lobby-agent-api.md`
- Modify: relevant Core/Agent compose or deployment environment documentation
- Modify: `docs/aegis/INDEX.md`

**Why this task exists:** Operators need the new token lifecycle, environment variables, reporting failure semantics, 180-day retention, endpoint filters and metric semantics to deploy safely.

**Impact / Compatibility:** Document that old agents keep working without statistics until recreate; never document or expose bearer secrets; preserve existing management token and network boundaries.

**Verification:** Search docs for endpoint/env names and verify each matches the implemented constants and routes; run a final `git diff --check`.

- [ ] **Step 1: Add failing documentation checklist assertions** as a bounded review checklist in `50-evidence.md`.
- [ ] **Step 2: Run the checklist before edits and record missing entries.
- [ ] **Step 3: Update API/deployment docs with exact request/response/filter and failure semantics.
- [ ] **Step 4: Re-run doc search and `git diff --check`.**

### Task 10: Full verification and advisory review

**Files:**
- Modify: `docs/aegis/work/2026-08-22-player-entry-statistics/50-evidence.md`

**Why this task exists:** This cross-layer feature can compile in one layer while violating a contract or user journey in another. Fresh evidence is required before claiming a complete implementation.

**Verification:** Run all .NET test projects, frontend tests/build, `dotnet build L4d2MatchmakingManager.sln`, migration/model checks, and inspect `git diff --check` and `git status`. Request an advisory code review focused on token scope, callback non-blocking behavior, SQL/timezone correctness, snapshot stability, frontend mounted route, and missing regression coverage.

- [ ] **Step 1: Run focused suites for every changed layer and capture exit codes/counts.
- [ ] **Step 2: Run the full .NET and frontend verification commands; record blockers honestly.
- [ ] **Step 3: Review the complete diff against the design and compatibility boundary.
- [ ] **Step 4: Dispatch advisory code review with exact scope, requirements and evidence; repair Critical/Important findings.
- [ ] **Step 5: Re-run affected and full verification after repairs and update `50-evidence.md` with residual risks/confidence.**

**Repair Track:** Canonical new owners are the reporting sink/uploader for event transport, `PlayerEntryEvent` for raw persistence, and `PlayerEntryStatisticsQuery` for aggregation. Existing Steam callback, scheduling and management auth owners stay in place; only their minimal context/registration seams change.

**Retirement Track:** No old statistics path exists. The runtime-probed region remains a health-display fallback only and is explicitly excluded from event snapshots; the management bearer remains active for management routes and is never reused for reporting. A future durable queue or aggregate archive may replace the in-memory/raw-only boundary only after a separate reliability/retention decision.

**Known baseline issue:** The first worktree baseline `dotnet test L4d2MatchmakingManager.sln --no-restore` could not run because this new worktree had no `obj/project.assets.json`; restore is required before interpreting test results.
