# Evidence

## Checkpoint

- **Current todo:** authenticated current warm-up status API.
- **Completed todos:** reservation-only RCON credentials.
- **Next step:** add a failing endpoint test for persisted active/uncertain
  warm-up status projection.

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
