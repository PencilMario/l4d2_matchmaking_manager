# Lobby Query Reliability and Membership Design

## Goal

Make the public Core lobby-query API return confirmed metadata rather than a
successful empty snapshot, and return lobby member identities when a selected
Warm-up Agent can temporarily join safely.

## Facts and Scope

- RequestLobbyData completes asynchronously through LobbyDataUpdate_t. The
  existing shared Steam runtime sends the request, waits approximately 250 ms,
  and then reads its local cache without observing the completion callback.
- Steam only permits a lobby member to enumerate other members' Steam IDs.
  Requesting lobby data without joining can read metadata but does not make
  member identities available.
- A Warm-up Agent holds at most one managed warm-up operation. An active
  standard operation may be selected only as a temporary query fallback;
  reserved and uncertain operations are never query fallbacks.

The scope is GET /v1/lobbies/{lobbyId}, Core's private Agent control
contract, the shared Steam session actor/runtime, focused tests, and both API
references. It does not add a public join/leave route or change normal
warm-up rules.

## Query Algorithm

1. Core validates the decimal non-zero lobby ID and obtains healthy Agents.
2. It selects an Agent with no active or uncertain Warm-up Attempt first. If
   none exists, it selects an Agent whose only current attempt is active with
   mode=standard. Reservation and uncertain attempts are excluded.
3. The Agent calls RequestLobbyData and waits for the matching
   LobbyDataUpdate_t success callback, with a five-second deadline. A false
   request result, failed callback, or deadline expiry is a query failure,
   never an empty successful snapshot.
4. For an eligible selected Agent, it calls JoinLobby and waits for the
   matching successful LobbyEnter_t. On success it reads the member list,
   removes the querying Agent's own Steam ID, and leaves the temporary lobby
   in a finally path.
5. If the query used an active standard Agent, it verifies after leaving that
   the original operation remains active and that the same Agent still holds
   its original lobby. A failed preservation check quarantines the Agent and
   does not silently claim its warm-up continues.

Agent commands remain serialized by its persistent actor. Core adds a
per-Agent query lease so concurrent HTTP callers do not queue multiple
temporary joins on the same Steam session. The actor remains the final guard:
if an Agent changed to a reservation or uncertain operation after Core
selected it, it declines the temporary join and returns metadata-only.

## Response Contract

LobbySnapshot is extended additively with:

~~~json
{
  "memberDataStatus": "complete"
}
~~~

members contains only target-lobby members other than the temporary query
Agent. Its array length is therefore the returned player count. Core does not
rewrite metadata["Members:numPlayers"]: that value is target-lobby owner
metadata and is not a reliable measurement made by this query.

Allowed memberDataStatus values are:

| Value | Meaning |
| --- | --- |
| complete | Lobby data was confirmed, the Agent joined, and members excludes that Agent. |
| metadata_only_no_query_agent | No idle or eligible standard Warm-up Agent was available. |
| metadata_only_join_denied | Steam denied the temporary join, such as a full or non-joinable lobby. |
| metadata_only_join_timeout | Steam did not complete the temporary join before its deadline. |
| metadata_only_agent_state_changed | The selected Agent was no longer eligible when its actor processed the request. |

For every metadata-only value, Core returns confirmed owner/metadata and an
empty members array. This is a 200 response because the metadata request
succeeded and the reason is present in memberDataStatus.

If the data refresh itself fails, Agent returns an explicit private query
failure and Core returns:

~~~http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json; charset=utf-8

"lobby_data_unavailable"
~~~

No healthy Agent remains a 503 availability failure. Existing callers that
only consume the original snapshot fields remain compatible; callers needing
member identities must branch on memberDataStatus.

## Safety and Compatibility

- The old fixed 250 ms, cache-only read is retired. There is one canonical
  LobbyDataUpdate_t-gated metadata refresh in the shared Steam runtime.
- The old claim that an Agent can obtain arbitrary lobby member identities
  without joining is retired. Metadata-only is explicit rather than
  fabricated as a complete member list.
- A temporary query never joins a Reservation Lobby Agent and never joins an
  Agent with an uncertain operation.
- A temporary join must always leave on success, failure, cancellation, and
  exception. It must not mutate target metadata, owner, game-server fields,
  or reservation state.
- The Core response continues to hide the identity of the Agent used for the
  query and exposes no Steam credentials or Docker identifiers.

## Verification

- Unit tests cover data-refresh success, timeout/failure mapping, status
  values, self filtering, idle-first selection, standard-only fallback, and
  exclusion of reservation/uncertain Agents.
- Actor tests cover JoinLobby success/denial/timeout, leave-in-finally, and
  original-standard-operation preservation failure.
- A controlled live integration test uses a standard warm-up Agent holding a
  test lobby, temporarily joins a second test lobby, leaves it, and confirms
  the original operation and lobby remain active. The fallback is not
  considered deployment-ready without this evidence.
- Core and Agent API documentation include the new response field and 503
  failure response.

## Non-goals

- Joining arbitrary lobbies from the browser, retaining a query membership,
  querying Reservation Lobby Agents, changing target lobby metadata, or
  altering the L4D2 owner-reported player-count metadata.
- A dedicated query-only Steam account pool. That remains a future option if
  temporary multi-lobby membership proves unsuitable in production.

## Draft Inputs

- **TaskIntentDraft:** replace pseudo-empty lobby snapshots with confirmed
  metadata and provide real member identities while accounting for the query
  Agent itself.
- **BaselineReadSetHint:** CONTEXT.md, Core lobby endpoint and agent control
  client, shared SteamSessionActor/SteamNativeRuntime, Agent API
  documentation, official Steam matchmaking API behavior, and the successful
  local Probe comparison.
- **ImpactStatementDraft:** Core selects the least disruptive eligible
  Warm-up Agent; the Agent owns all Steam mutation and cleanup; the shared
  Steam runtime owns callback completion and membership filtering. The public
  API changes additively except that an unconfirmed data request becomes an
  explicit 503 instead of 200 with empty data.
