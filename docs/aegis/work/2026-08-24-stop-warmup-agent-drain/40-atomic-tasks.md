# Atomic Tasks

- [ ] Add endpoint integration coverage for successful per-Agent drain before container stop.
- [ ] Add endpoint integration coverage for failed drain returning `409` without stopping the container.
- [ ] Implement the drain gate in `WarmupAgentService.StopCoreAsync`.
- [ ] Map `warmup_agent_stop_drain_failed` to `409` in `WarmupAgentEndpoints.StopAsync`.
- [ ] Update both Core API documents with stop-drain semantics.
- [ ] Run focused tests, Core tests, solution tests, and `git diff --check`.
- [ ] Write verification evidence.
