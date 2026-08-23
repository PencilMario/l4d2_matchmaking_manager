# Verification Evidence

## TDD Evidence

- Core RED: `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter "FullyQualifiedName~TargetServerEndpointTests"` executed 13 tests; 11 passed and the two new tests failed because negative priority returned `201 Created`/`200 OK` instead of `400 BadRequest`.
- Frontend RED: `npm test -- --run src/features/servers/TargetServerDrawer.test.tsx src/components/TargetServersView.test.tsx` executed the new assertions; both failed because the priority inputs had no `min="0"` attribute.
- Core GREEN: the focused suite passed 13/13 after adding the shared `Normalize` validation.
- Frontend GREEN: the final focused target-server suite passed 6/6 after moving the workbench test to `TargetServerForm.test.tsx` and adding legacy Modal rejection coverage.

## Final Verification

- `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --logger "console;verbosity=minimal"`: 203 passed, 0 failed, 3 existing skipped, 206 total.
- `npm test -- --run`: 24 test files passed; 84 tests passed, 0 failed.
- `npm run build`: TypeScript compilation and Vite production build exited 0.
- `git diff --check`: exited 0.
- Negative-priority wording scan across current Core/frontend/docs owners: no remaining “允许负数/可以为负数/可为负数” matches.

## Compatibility and Scope

- `priority = null` still defaults to 0.
- Priority 0 and positive integers retain the existing API, persistence, sorting, and scheduling behavior.
- No migration was added or executed; existing instances were not rewritten.
- The untracked `docs/gemini-tab-ui-optimization-brief.md` from the base workspace was not copied or modified.
- No fallback, adapter, or duplicate validation owner was introduced.

## Residual Risk

- No live deployed Core/API or browser E2E run was performed; automated unit/component, integration HTTP, and production-build coverage passed.
- `npm ci` reported five dependency audit findings and one deprecation warning; dependency maintenance is outside this change.
