# Verification Evidence

## TodoCheckpointDraft

- Completed: CoreSettings persistence, migration, dedicated API, global drain, scheduler gate, both frontend API clients, both settings UIs, App/WorkspaceShell wiring, `restarting` status mapping, API docs.
- Active slice: completed final diff audit and residual-risk review.
- Next: preserve the existing worktree changes and hand off the verified result.
- Blocked-on: none.

## EvidenceBundleDraft

Commands run from the isolated worktree `global-warmup-disable`:

| Check | Result |
| --- | --- |
| `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "FullyQualifiedName~GlobalSettingsEndpointTests\|FullyQualifiedName~WarmupAttemptDrainServiceTests\|FullyQualifiedName~WarmupSchedulerServiceTests" --no-restore` | 40 passed, 0 failed |
| `dotnet test L4d2MatchmakingManager.sln` | 133 passed, 3 skipped, 0 failed |
| `npm test -- --run` in `frontend/` | 51 passed, 0 failed |
| `npm test -- --run src/api/core-client.test.ts src/components/GlobalSettingsView.test.tsx src/features/settings/SettingsWorkspace.test.tsx src/features/workspace/WorkspaceShell.test.tsx` | 19 passed, 0 failed |
| `npm run build` in `frontend/` | TypeScript and Vite build exited 0 |
| `git diff --check` | exited 0 with no output |

## DriftCheckDraft

- Scope: remains limited to the persistent global warm-up/scheduling switch, safe task drain, scheduler pause, UI confirmation, API clients, and contract docs.
- Compatibility: legacy `/v1/settings` remains mapped and does not write the new flag; five-second polling and Bearer authentication remain unchanged.
- Container boundary: the new drain path calls Agent operation stop and Steam recovery only; it does not stop, delete, or recreate Agent containers.
- Retirement: no route was removed; the dedicated resource is the active frontend owner, while the combined settings PUT remains a legacy compatibility path.
- Decision: `continue`; no blocking drift found.

## Residual Risk

- No live PostgreSQL/Docker deployment or browser Playwright journey was run in this worktree. Automated tests use the repository's existing test hosts and fakes.
- The production database migration should be observed during the normal deployment rollout before changing the switch on a live Core.
