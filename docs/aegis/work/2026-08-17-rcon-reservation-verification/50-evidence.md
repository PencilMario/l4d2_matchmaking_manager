# RCON Reservation Verification Evidence

## Automated Verification

- `dotnet test L4d2MatchmakingManager.sln --no-restore`: passed. Core: 50
  passed, 3 PostgreSQL/Testcontainers cases skipped locally because Docker is
  unavailable; Protocol: 11 passed; SteamLobbyProbe: 5 passed; Agent: 15
  passed.
- `dotnet build L4d2MatchmakingManager.sln --warnaserror --no-restore`:
  passed with zero warnings and zero errors.
- `pwsh -NoProfile -File deploy/matchmaking-core/Test-ComposeContract.ps1`:
  passed. The contract requires the RCON encryption key file and rejects a
  plaintext key environment variable.
- Source RCON fixture verifies successful authentication and accepts only an
  exact `reserved <current-lobby-cookie>` token. Agent decision tests verify
  that only the UDP timeout result can use this fallback.
- Scheduler integration verifies that the decrypted password reaches only the
  private reserved Agent request.

## Remote Deployment

- Rebuilt the Agent and Core images on the single Docker host, provisioned a
  mode-0600 Docker secret file for the AES-GCM key, and restarted only the
  Core Compose service. The pre-existing unrelated standalone Agent was not
  changed.
- Recreated the one Core-managed logged-in Agent; its account volume remained
  intact and the internal health endpoint returned `ready=true`.
- A temporary reservation target was created through the Core API. Its read
  response reported `hasRconCredentials=true`, without returning a password
  or ciphertext.
- The managed Agent created and held an active lobby with an official campaign
  profile. Source RCON `status` confirmed the server reservation token exactly
  matched that lobby cookie. Core's public lobby-query API returned the live
  lobby metadata and did not expose an RCON password field.
- Cleanup stopped the operation, disabled and deleted the temporary target,
  and confirmed the server had no remaining reservation token. Final database
  counts were zero Target Servers, zero unfinished attempts and zero
  reservation leases; Core and the managed Agent remained healthy.

## Drift Check

- Scope remains the single-host Core and its private Docker-network Agents.
  No public Agent endpoint, plaintext credential persistence, or RCON
  administration command was added.
- The old behavior remains for explicit UDP rejection and targets without
  RCON credentials. The only new success path is an exact server-side status
  match after the documented ambiguous timeout.
