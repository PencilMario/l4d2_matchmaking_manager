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

## Task 2: Settings API

- RED: `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~GlobalSettingsEndpointTests --no-restore`
  - Expected failures: 5 new tests because `/v1/settings/warmup-pause-windows` was not mapped and the combined response had no new fields.
- GREEN: the same focused command after implementing the DTO, JSON persistence, dedicated routes, immediate drain, and conflict mapping
  - Passed: 15; skipped: 0; failed: 0.

## Task 3: Scheduler effective pause

- RED: `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupSchedulerServiceTests --no-restore`
  - New pause tests exposed two failures: `TickCoreAsync` proceeded into the scheduling path and did not drain current attempts.
- Root cause: `RecoverCoreAsync` already drained on the effective-pause early return, while `TickCoreAsync` only returned. The canonical scheduler guard therefore enforced “do not start” but not “stop existing tasks” during a time window.
- GREEN: the same focused scheduler command after adding the shared drain branch to `TickCoreAsync`
  - Passed: 46; skipped: 0; failed: 0.
- Cross-slice regression: combined Core filter for rules, settings, drain, and scheduler
  - Passed: 71; skipped: 0; failed: 0.

## Task 4: Frontend API contracts and clients

- RED: `npm test -- --run src/api/core-client.test.ts`
  - Expected failure: `client.getWarmupPauseWindows is not a function`.
- GREEN: the same focused command after adding the typed models and dedicated client methods
  - Passed: 8 tests; failed: 0.
- Compatibility build: `npm run build`
  - Passed: TypeScript compilation and Vite production build; failed: 0.
- Build fixture repair: the new required `GlobalSettings` fields were added to the existing `SettingsWorkspace` test fixtures; the targeted settings/API tests then passed 14/14.

## Checkpoint

- Completed: worktree setup, approved design, implementation plan, Task 1 rules and migration model.
- Completed: Task 4 frontend API contracts and clients.
- Blockers: none.
- Drift check: implementation remains within the approved CoreSettings JSON, Asia/Shanghai, drain-on-pause, and independent-settings-card scope; no new owner or fallback was introduced.
- Next: write failing component tests for the two settings entry points, then implement four independent cards and the multi-window editor.

## Task 5: Two settings surfaces and independent cards

- RED: `npm test -- --run src/components/GlobalSettingsView.test.tsx src/features/settings/SettingsWorkspace.test.tsx`
  - Expected failures: the existing monolithic markup had no four card headings, pause-window empty state, add/remove controls, or dedicated pause callback.
- GREEN: the same focused command after implementing the shared minute-level editor and four sibling cards
  - Passed: 17 tests; failed: 0.
- Wiring regression: `npm test -- --run src/features/workspace/WorkspaceShell.test.tsx src/api/core-client.test.ts`
  - Passed: 10 tests; failed: 0. Both dedicated pause update paths are passed through the shell and refresh the snapshot.
- Full frontend regression: `npm test -- --run`
  - Passed: 21 test files, 68 tests; failed: 0.
- Build: `npm run build`
  - Passed: TypeScript compilation and Vite production build; failed: 0.
- Scope: both settings entry points now render four same-level cards; pause editing supports multiple rows, cross-midnight values, deletion, empty-array saves, local validation, Asia/Shanghai copy, independent saving, and no secret echo.

## Checkpoint

- Completed: Task 5 frontend cards, editor, and application wiring.
- Blockers: none.
- Drift check: legacy combined settings GET remains initialization-only; pause writes use the dedicated resource; Agent containers remain outside the UI behavior.
- Next: diagnose and close the intermittent editor reset found during final frontend verification, then update the API documents and rerun the final Core suite, frontend suite, build, and diff checks.

## Task 6: Editor draft preservation regression

- Symptom: during one full Vitest run, `SettingsWorkspace.test.tsx` could not find `开始时间 1` immediately after `新增时间段`; the DOM had returned to `暂未设置暂停时间段`.
- Root-cause evidence: `WarmupPauseWindowsForm` owned the editable draft in local state but also copied `initialWindows` and `initialActive` into that state from an effect. A parent rerender with an equal-value, new-array settings snapshot therefore overwrote an unsaved row. The parent settings state is the canonical persisted snapshot; the form's local state is the canonical owner while editing, and the save callback already applies the API response explicitly.
- RED: added `frontend/src/components/settings/WarmupPauseWindowsForm.test.tsx`, then ran `npm test -- --run src/components/settings/WarmupPauseWindowsForm.test.tsx`
  - Failed as expected: after rerendering with unchanged settings, `开始时间 1` was missing.
- GREEN: replaced unconditional prop-to-draft synchronization with value-aware, dirty-gated synchronization and retained explicit API-response synchronization after save; the same focused command
  - Passed: 3 tests; failed: 0.
- Related settings regression: `npm test -- --run src/components/settings/WarmupPauseWindowsForm.test.tsx src/features/settings/SettingsWorkspace.test.tsx src/components/GlobalSettingsView.test.tsx`
  - Passed: 3 test files, 20 tests; failed: 0.

## Task 7: Review repair for pause transitions and editor ownership

- Review finding verified: when a drain failed during an active pause, clearing the window previously returned `200` without retrying the retained attempt. A separate endpoint test reproduced that path.
- RED: `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~LeavingAnActivePauseWindowDrainsBeforeRestoringScheduling`
  - Failed as expected: the second update returned `200` but the retained operation was not stopped.
- GREEN: an active-to-inactive transition now drains before saving; a failed drain preserves the old pause configuration and returns `409`, while a successful retry saves the new configuration. The focused endpoint test passed: 1; failed: 0.
- Review finding verified: after removing the original synchronization effect, changed external settings did not reach a clean editor. The form regression now covers clean sync, dirty draft preservation, and save-time input locking.
- RED/GREEN: the input-lock test failed before `disabled={saving}` was restored, then `npm test -- --run src/components/settings/WarmupPauseWindowsForm.test.tsx` passed 3 tests; failed: 0.
- Affected-slice regression: Core rules/settings/scheduler filter passed 69; frontend API, wiring, both settings surfaces, and form tests passed 30; `npm run build` passed.

## Advisory review

- Review range: `8eec9d1..f242da4`, read-only review by the delegated reviewer.
- Assessment: no Critical issues. Two Important issues were reproduced and repaired: pause-exit drain could be bypassed after a prior conflict, and the editor lacked a clean/dirty external snapshot boundary. A Minor input-lock issue and inconsistent `active` examples were also repaired.
- Residual review notes: PostgreSQL/Testcontainers coverage remains skipped in this environment; no browser-level visual/E2E run or live deployment verification was performed. Serializer ownership is still duplicated between settings and scheduler readers but is outside this slice's minimum repair.

## Final verification after review repair

- Core: `dotnet test L4d2MatchmakingManager.sln`
  - Passed: 175; skipped: 3; failed: 0.
- Frontend: `npm test -- --run`
  - Passed: 22 test files, 71 tests; failed: 0.
- Frontend build: `npm run build`
  - Passed: TypeScript compilation and Vite production build; failed: 0.
- Diff hygiene: `git diff --check`
  - Passed: no whitespace errors.

## Final Checkpoint

- Completed: implementation, API contract documentation, four-card UI, editor and pause-transition review repairs, and full automated verification.
- Blockers: none.
- Drift check: review repairs strengthen the existing drain and editor owners without adding a fallback path. The dedicated pause API and Agent-container boundary remain unchanged.
- Next: inspect and commit the final repair/documentation diff, then present branch integration options.
