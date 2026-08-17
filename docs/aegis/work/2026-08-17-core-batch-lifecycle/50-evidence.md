# Evidence

## Checkpoint

- **Current todo:** bounded multi-Agent scheduler batch planning.
- **Completed todos:** reservation-only RCON credentials; authenticated current
  warm-up status API.
- **Next step:** establish full scheduler-test and configuration baseline, then
  add a failing multi-Agent batch test.

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
