# Verification Evidence

## TodoCheckpointDraft

- Completed: Agent request contract, atomic target-file writer, Agent internal route, Core HTTP client, running/restarting/stopped/unchanged/failure update behavior, stopped-start application, test doubles, empty-target Bridge authority, frontend error mapping, API/deployment docs, and full verification.
- Active slice: final handoff review.
- Next: preserve the isolated branch or integrate it into the selected base branch.
- Blocked-on: none for automated verification; live Steam/Millennium deployment remains unverified.

## EvidenceBundleDraft

Commands run from the isolated worktree `agent-download-region-restart`:

| Check | Result |
| --- | --- |
| `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --no-restore --verbosity minimal` | 29 passed, 0 failed |
| `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --verbosity minimal` | 209 passed, 3 skipped, 0 failed |
| `dotnet test L4d2MatchmakingManager.sln --no-restore --verbosity minimal -m:1` | Contracts 7 passed; Protocol 13 passed; SteamLobbyProbe 24 passed; Agent 29 passed; Core 209 passed, 3 skipped; 0 failed |
| `dotnet build L4d2MatchmakingManager.sln --no-restore --verbosity minimal` | 0 warnings, 0 errors |
| `npm test -- --run` in `frontend/` | 25 test files, 82 tests passed, 0 failed |
| `npm run build` in `frontend/` | TypeScript and Vite build exited 0 |
| `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-MillenniumRegionBridgeContract.ps1` | exited 0 |
| `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-ComposeContract.ps1` | exited 0 |
| `pwsh -NoProfile -File deploy/matchmaking-core/Test-ComposeContract.ps1` | exited 0 |
| `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamAccountConfiguration.ps1 -SharedLibraryPath deploy/steam-lobby-agent/tests/fixtures/shared-library` | exited 0 |
| `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamWebHelperGuardContract.ps1` | exited 0 |
| `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamStatusLauncher.ps1` | exited 0 |
| `git diff --check` | exited 0 with no output |

The first parallel solution test/build attempt hit a shared MSBuild static-web-assets cache lock; the solution test and build were rerun serially with `-m:1` and passed.

## DriftCheckDraft

- Scope: remains limited to applying changed Steam download regions on Warm-up Agents, restarting Steam, preserving default-region semantics, frontend error display, and the related API/deployment documentation.
- Compatibility: existing `POST /v1/steam/restart` remains unchanged; container create/start/stop/delete/recreate and account volume behavior remain unchanged.
- Runtime owner: the Agent owns the account-local Millennium target file; Core owns the running-vs-stopped decision and database persistence boundary.
- Retirement: manual `recreate` after a region edit is retired; `recreate` remains documented for image, Steam API path, and login UI mode changes.
- Decision: continue; no blocking drift found; the restarting-state edit path is covered by a regression test.

## Residual Risk

- No live Docker/Steam/Millennium Agent deployment was available, so actual `supervisorctl restart steam`, Steam region application, and post-restart health recovery still need runtime verification.
- The frontend dependency install reported existing npm audit findings (1 critical, 3 high, 1 moderate); dependency versions were not changed by this task.
- The initial solution build required `dotnet restore` because three unrelated test projects lacked worktree-local assets; the post-restore build and full solution tests passed.

## Review Outcome

- Independent read-only review was requested for the Agent controller, Core update boundary, Bridge fallback, compatibility paths, and tests, but the subagent timed out twice and was closed without a report.
- A local read-only diff review found no additional blocking issue; this remains advisory and must not be treated as a production deployment approval.

## Confidence

- Grade: B. Direct unit/integration/contract/build evidence covers the changed code paths and compatibility boundaries; the live Steam/Millennium runtime remains an explicit unknown.
- Evidence is advisory verification only, not an authoritative production deployment completion signal.
