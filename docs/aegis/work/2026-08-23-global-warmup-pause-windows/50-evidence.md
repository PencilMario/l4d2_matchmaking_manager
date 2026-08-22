# Verification Evidence

## Baseline

- `dotnet test L4d2MatchmakingManager.sln --no-restore`
  - Passed: 159; skipped: 3; failed: 0.
- `npm test -- --run`
  - Passed: 21 test files, 60 tests; failed: 0.

## Task 1: Rules and persistence model

- RED: `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupPauseWindowRulesTests --no-restore`
  - Expected compile failure because the new rule type and methods did not exist.
- GREEN: the same focused command
  - Passed: 7; skipped: 0; failed: 0.
- Build: `dotnet build src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj --no-restore`
  - Passed: 0 warnings, 0 errors.

## Checkpoint

- Completed: worktree setup, approved design, implementation plan, Task 1 rules and migration model.
- Active slice: Task 2 Settings API and persistence service.
- Blockers: none.
- Drift check: implementation remains within the approved CoreSettings JSON, Asia/Shanghai, drain-on-pause, and independent-settings-card scope; no new owner or fallback was introduced.
- Next: write failing endpoint contract tests for the dedicated pause-window resource and legacy compatibility.
