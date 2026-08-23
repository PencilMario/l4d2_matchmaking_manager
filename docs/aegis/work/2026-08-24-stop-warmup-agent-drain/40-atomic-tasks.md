# Atomic Tasks

- [x] Add endpoint integration coverage for successful per-Agent drain before container stop.
- [x] Add endpoint integration coverage for failed drain returning `409` without stopping the container.
- [x] Implement the drain gate in `WarmupAgentService.StopCoreAsync`.
- [x] Map `warmup_agent_stop_drain_failed` to `409` in `WarmupAgentEndpoints.StopAsync`.
- [x] Update both Core API documents with stop-drain semantics.
- [x] Run focused tests, Core tests, solution tests, and `git diff --check`.
- [x] Write verification evidence.
