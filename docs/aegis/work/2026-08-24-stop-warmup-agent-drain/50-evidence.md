# Verification Evidence

## TDD red-green evidence

### RED

After adding the two endpoint tests and before changing production code:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupAgentEndpointTests --no-restore --logger 'console;verbosity=minimal'
```

Result: 15 passed, 2 failed. The success case observed zero stopped operations while
the current implementation returned `200`; the failure case observed `200` instead of
the expected `409`.

After adding the retry assertion and before allowing `quarantined` nodes through the
stop gate, the focused retry test failed with expected status `stopped` but actual
status `quarantined`.

### GREEN

After adding drain-before-stop, the stop-drain conflict mapping, and the quarantined
retry condition:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupAgentEndpointTests --no-restore --logger 'console;verbosity=minimal'
```

Result: 17 passed, 0 failed.

## Regression evidence

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --logger 'console;verbosity=minimal'
```

Result: 205 passed, 0 failed, 3 existing PostgreSQL environment tests skipped.

```powershell
dotnet test L4d2MatchmakingManager.sln --no-restore --logger 'console;verbosity=minimal'
```

Result: Contracts 7 passed, Protocol 13 passed, SteamLobbyProbe 24 passed,
LobbyAgent 25 passed, Core 205 passed; 0 failed and 3 existing PostgreSQL environment
tests skipped.

```powershell
git diff --check
```

Result: no output and exit code `0`.

## Review evidence

- The implementation commit is `d89828d`, based on design commit `30b4b9a`.
- An independent code-review agent was dispatched with the implementation, plan,
  compatibility boundary, and test evidence. It did not return after two wait windows
  and was closed; no reviewer approval is claimed.
- Local review checked the old direct-stop path is replaced, `DrainAgentAsync` remains
  the only task-drain owner, the endpoint catches only the stop-drain code, and failed
  attempts cannot reach container stop.

## Residual risk

- The full PostgreSQL/Testcontainers cases were skipped by their existing environment
  conditions; the changed behavior is covered by the EF Core InMemory endpoint tests.
- No frontend source was changed, so browser-level rendering was not applicable. The
  existing UI calls the unchanged stop endpoint and receives the documented `409`.
