# Evidence

## Checkpoint

- **Current todo:** drain active operations before disabling or deleting a
  Target Server.
- **Completed todos:** reservation-only RCON credentials; authenticated current
  warm-up status API; bounded multi-Agent scheduler batch planning.
- **Next step:** add failing Target Server lifecycle tests, then extract the
  confirmed-stop/quarantine drain owner.

## Task 1

- **Behavior:** normal targets reject a supplied RCON password; converting a
  reservation target to normal clears the stored credential; standard Agent
  starts never decrypt or send a credential.
- **Red evidence:** the new endpoint and scheduler tests failed before the
  implementation because normal targets were created and standard starts
  decrypted ciphertext.
- **Green evidence:** `dotnet test
  src\\L4d2MatchmakingCore\\tests\\L4d2MatchmakingCore.tests.csproj
  --no-restore --filter "FullyQualifiedName~TargetServerEndpointTests|FullyQualifiedName~WarmupSchedulerServiceTests"`
  passed 22 tests.
- **Drift check:** scope remains configuration validation and Agent request
  construction. No Agent protocol, secret storage design, or fallback path was
  added. Decision: continue.

## Task 2

- **Behavior:** authenticated `GET /v1/warmups` returns only persisted active
  and uncertain attempts with target/Agent display data and timing fields. It
  omits sensitive configuration and account/container data.
- **Red evidence:** the endpoint test received `404` before route mapping.
- **Green evidence:** `dotnet test
  src\\L4d2MatchmakingCore\\tests\\L4d2MatchmakingCore.tests.csproj
  --no-restore --filter "FullyQualifiedName~WarmupStatusEndpointTests|FullyQualifiedName~CoreAuthenticationTests|FullyQualifiedName~LobbyQueryEndpointTests|FullyQualifiedName~TargetServerEndpointTests|FullyQualifiedName~WarmupSchedulerServiceTests"`
  passed 26 tests.
- **Drift check:** the new scoped service only reads the Core database and does
  not depend on Agent or A2S clients. Existing management and lobby routes
  retain their contracts. Decision: continue.

## Task 3

- **Behavior:** one tick plans a bounded batch over ready idle Agents, reserves
  capacity before Agent calls, and only advances persistent round-robin cursors
  for successful starts. Reservation leases remain exclusive.
- **Red evidence:** two ready Agents initially produced one Agent start, and
  `CoreOptions` had no scheduler start-limit property.
- **Green evidence:** `dotnet test
  src\\L4d2MatchmakingCore\\tests\\L4d2MatchmakingCore.tests.csproj
  --no-restore` passed 55 tests; 3 existing PostgreSQL-dependent tests were
  skipped by their established precondition.
- **Debug finding:** the RCON tests used reflection with a fixed seven-argument
  constructor call after the scheduler gained an optional `CoreOptions`
  parameter. The test helper is the canonical compatibility seam and now passes
  the omitted optional argument explicitly. Its focused regression passed.
- **Drift check:** Core remains the only scheduler/state owner. Agent requests
  are still private and independent; no new persistence model or Agent protocol
  was introduced. Decision: continue.
