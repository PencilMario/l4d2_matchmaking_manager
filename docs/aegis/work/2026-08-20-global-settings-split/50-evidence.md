# Verification Evidence

## Scope

VNC proxy and Steam Web API Key updates now use independent authenticated Core resources and independent UI forms. The legacy combined settings resource remains for compatibility.

## Commands

- `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~GlobalSettings`
  - Passed: 6, failed: 0.
- `npm test -- --run src/components/GlobalSettingsView.test.tsx src/features/settings/SettingsWorkspace.test.tsx src/api/core-client.test.ts`
  - Passed: 11, failed: 0.
- `npm run build`
  - Passed: TypeScript project build and Vite production build.
- `dotnet test L4d2MatchmakingManager.sln`
  - Passed: Contracts 2, Protocol 11, SteamLobbyProbe 15, LobbyAgent 19, Core 123; Core skipped 3 existing tests; no failures.
- `npm test -- --run`
  - Passed: 16 files, 43 tests, no failures.
- `git diff --check`
  - Pending final pre-commit execution.

## Main User Journey

Component tests exercise both settings implementations: saving VNC proxy only invokes the proxy callback; saving and clearing the API Key only invoke the key callback. Endpoint tests verify proxy/key updates and key clearing preserve the other setting.

## Residual Risk

No live Core deployment/browser E2E was run for this change. The production build and unit/integration tests cover the compiled frontend and Core contract, but a deployed instance must be updated before the new UI can use the dedicated routes.
