# Baseline Read Set

- `docs/aegis/specs/2026-08-19-standard-warmup-continuation-design.md`: approved behavior and compatibility boundary.
- `CONTEXT.md`: Target Server, Warm-up Attempt Window, priority, and A2S authority terminology.
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`: allocation orchestration and persisted attempt creation.
- `src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs`: target selection and lifecycle decisions.
- `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`: integration-style scheduler test patterns and fakes.
- `frontend/src/components/OverviewView.tsx`: latest server-grouped monitoring UI.
- `frontend/src/services/api.ts`: backend warmup response adapter.
