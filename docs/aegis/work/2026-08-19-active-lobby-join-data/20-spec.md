# Active Lobby JoinData Repair

## Intent

Make a Warm-up Agent create an L4D2-recognized Steam game session and answer
real-client JoinData requests for its current active lobby.

## Canonical Owner

`SteamNativeRuntime` remains the only owner of native Steam lobby metadata,
game-server binding, callback pumping, and outbound JoinData replies. The
single `SteamSessionActor` remains the only caller of that runtime.

## Required Behavior

When `CreateLobby` receives an IPv4 target and port, it must write these
metadata fields in addition to the existing L4D2 session profile:

| Key | Value |
| --- | --- |
| `server:adrlocal` | `<ipv4>:<port>` |
| `server:adronline` | `<ipv4>:<port>` |
| `server:connectstring` | `<ipv4>:<port>` |
| `server:reservationid` | Decimal ID of the newly-created Steam lobby |

The existing `SetLobbyGameServer` call stays in place and uses the same target
endpoint.

While an Agent operation is active, the callback pump must accept a
`RequestJoinData` only when its target lobby ID is the active operation's
lobby. It must send a `ReplyJoinData` whose Server section uses the active
lobby's endpoint and reservation ID. Requests for all other lobby IDs must be
ignored and must not change the active operation.

## Compatibility Boundary

- Existing Agent HTTP API and Core scheduler contracts remain unchanged.
- Standard and reserved operations both receive the four metadata fields.
- A non-active lobby never receives a synthesized L4D2 response from this
  Agent.
- `SetLobbyGameServer` remains the Steam-native lobby binding; the new fields
  do not replace it.

## Verification

Unit tests prove the exact four metadata values and request filtering. A
controlled deployment verifies the new active lobby can be queried with all
fields present and captures whether a real L4D2 URI join reaches the Agent's
JoinData handler. The client join is not claimed successful without that
runtime evidence.

## Non-goals

- Do not answer JoinData for arbitrary queryable lobbies.
- Do not add a second Steam API process, HTTP callback endpoint, or manual
  lobby-creation API.
- Do not alter reservation UDP or RCON verification semantics.
