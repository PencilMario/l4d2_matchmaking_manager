# Reliable Lobby Query Atomic Task Log

- [x] Task 1: Add additive `memberDataStatus` contract and self-filter test.
  RED: `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter SteamSessionActorQueryTests` failed because the native join result and actor query contract did not exist. GREEN: the same focused suite passed with 7 tests.
- [x] Task 2: Add callback-confirmed metadata and native join result tests.
  Evidence: `SteamNativeRuntime` now waits for matching callback 505 before cache reads; the Probe suite passed with 13 tests and `dotnet build research/SteamLobbyProbe/SteamLobbyProbe.csproj --no-restore` completed with 0 warnings and 0 errors.
- [x] Task 3: Add actor temporary-join cleanup and preservation tests.
  RED: `QueryFailsTheStandardOperationWhenLeavingTheTemporaryLobbyThrows` failed because the original operation remained active. GREEN: it passed after the finally path marked the operation failed and wrapped the error as `lobby_operation_preservation_failed`.
- [x] Task 4: Add Agent route and known-failure mapping tests.
  RED: `AgentLobbyEndpointTests` could not access the stable Steam runtime failure. GREEN: `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj` passed with 16 tests; the route returns `503 "lobby_data_unavailable"` for an unconfirmed refresh.
- [x] Task 5: Add Core selection, lease, quarantine, and endpoint tests.
  RED: Core tests showed the old endpoint made no query-service call. GREEN: `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj` passed with 65 tests and 3 pre-existing database-dependent skips. It covers idle-first selection, standard fallback, metadata-only reservation fallback, per-Agent lease exclusion, error mapping, and quarantine.
- [x] Task 6: Update API documents and full regression/build evidence.
  Documentation now defines all statuses, self-filtered player count, and explicit 503 bodies. The Probe, Agent, and Core production builds completed with 0 warnings and 0 errors.

Pending controlled live evidence: deploy to the designated host, query a disposable second lobby from a standard warm-up Agent, verify returned members exclude that Agent, and verify its original operation remains active. Do not use a reservation Agent.

Each completed item records the exact RED and GREEN commands and their exit status below it. This log must not contain credentials, tokens, raw Steam logs, or Docker account-volume names.
