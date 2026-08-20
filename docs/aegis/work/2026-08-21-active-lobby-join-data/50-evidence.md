# Active Lobby JoinData Restoration Evidence

## Root Cause

`main` had no active-lobby JoinData responder, no `server:*` metadata merge, and
`SteamNativeRuntime.PumpCallbacks()` freed every callback without handling
callback 507. The complete repair existed only as commit `46b5239` on the
separate `fix/active-lobby-join-data` branch.

## RED/GREEN Evidence

- Responder RED: the focused test failed with `CS0246` because
  `ActiveLobbyJoinDataResponder` was absent.
- Responder GREEN: the focused suite passed 2/2 after adding the minimal
  active-lobby filter and delegating to `TryCreateRealReply`.
- Metadata RED: the focused test failed with `CS0117` because
  `SteamNativeRuntime.CreateServerMetadata` was absent.
- Metadata/JoinData GREEN: the focused suite passed 3/3 after restoring the
  dynamic metadata helper, responder lifecycle, callback 507 handling, and
  callback cleanup.

## Regression Verification

Commands were run serially to avoid concurrent .NET output locks:

| Command | Result |
| --- | --- |
| `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --no-restore --verbosity minimal` | 18 passed, 0 failed |
| `dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --no-restore --verbosity minimal` | 13 passed, 0 failed |
| `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --no-restore --verbosity minimal` | 20 passed, 0 failed |
| `dotnet build L4d2MatchmakingManager.sln --no-restore` | 0 warnings, 0 errors |
| `dotnet publish src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -c Release -r linux-x64 --self-contained false --no-restore` | published successfully after RID restore |

## Runtime Values Covered

The metadata test verifies these exact runtime-shaped values:

```text
server:adrlocal    = 106.54.197.3:24561
server:adronline   = 106.54.197.3:24561
server:connectstring = 106.54.197.3:24561
server:reservationid = 109775242646646147
```

The test fixture also sends a valid `SysSession::RequestJoinData` and verifies
that a matching active lobby produces a non-empty real reply while another
lobby is ignored.

## Residual Risk

No live Steam/L4D2 deployment was performed in this worktree. The automated
evidence proves the protocol/runtime wiring and build output, but does not claim
that a real client completed the final game-server connection. That requires a
controlled Agent deployment, a real `RequestJoinData` callback, and observation
of the client's connection result.
