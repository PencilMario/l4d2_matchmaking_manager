# Baseline Read Set

## Authority and context

- `CONTEXT.md`
- `docs/frontend-redesign-spec.md`
- `docs/matchmaking-core-frontend-api.md`
- `docs/matchmaking-core-api.md`
- `docs/aegis/specs/2026-08-19-standard-warmup-continuation-design.md`
- `docs/aegis/specs/2026-08-21-warmup-target-concentration-design.md`

## Core owners

- `src/L4d2MatchmakingCore/Data/Entities.cs`
- `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsDtos.cs`
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsEndpoints.cs`
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulingGate.cs`
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- `src/L4d2MatchmakingCore/Scheduling/WarmupAttemptDrainService.cs`

## Frontend owners

- `frontend/src/components/GlobalSettingsView.tsx`
- `frontend/src/features/settings/SettingsWorkspace.tsx`
- `frontend/src/api/models.ts`
- `frontend/src/api/core-client.ts`
- `frontend/src/services/api.ts`
- `frontend/src/state/useCoreSnapshot.ts`
- `frontend/src/App.tsx`
- `frontend/src/features/workspace/WorkspaceShell.tsx`

## Existing test baseline

- `dotnet test L4d2MatchmakingManager.sln --no-restore`: 159 passed, 3 skipped, 0 failed.
- `frontend`: 21 test files, 60 tests passed.

## Compatibility boundary

The legacy `/v1/settings` resource, existing manual `WarmupSchedulingEnabled` switch, five-second
scheduler cadence, task drain semantics, and Agent container lifecycle remain stable. New time-window
writes use a dedicated resource and never write secrets.
