# Core Batch Scheduling and Lifecycle Design

## Goal

Improve the single-host Core Controller for many Warm-up Agents by exposing
only current warm-up status, scheduling multiple eligible Agents per tick, and
draining active operations safely before a Target Server is disabled or
deleted.

## Scope

### Reservation Credentials

- `rconPassword` is valid only when `requiresReservation` is true.
- A create or update request for a non-reservation Target Server with a
  non-null `rconPassword` returns `400 rcon_requires_reservation`.
- Converting a reservation Target Server to a non-reservation server clears
  its ciphertext. The scheduler never decrypts or includes a credential in a
  standard Agent operation request.
- The existing AES-GCM at-rest encryption and private Core-to-Agent transport
  remain unchanged for reservation servers.

### Current Warm-up Status API

`GET /v1/warmups` is protected by the existing Core Bearer authentication and
returns only database attempts whose state is `active` or `uncertain`. Each
entry contains the Target Server endpoint and ID, Warm-up Agent display name
and ID, operation ID, lobby ID, mode, phase, lifecycle timestamps, deadline
and remaining seconds. It omits RCON credentials/ciphertext, Docker data,
Steam credentials and completed history.

The route is a read-only scheduler snapshot. It does not send fresh A2S or
Agent HTTP requests, so a status request cannot create load proportional to
the entire Agent fleet.

### Bounded Batch Scheduler

- The healthy-agent selector returns all ready Agents. Existing single-Agent
  selection remains available to consumers such as lobby queries.
- At the end of a tick, Core excludes Agents with an active or uncertain
  attempt, plans work for the remaining healthy Agents, and respects existing
  target priority, persistent same-priority round robin, reservation
  admission, target capacity and reservation exclusivity rules.
- Recreate-same-target requests are planned before ordinary global scheduling;
  each recreation request consumes exactly one Agent slot.
- Core writes planned attempts and reservation leases before it asks Agents to
  create lobbies. It then performs independent Agent start requests in
  parallel and applies results to the DbContext sequentially.
- `CORE_SCHEDULER_MAX_STARTS_PER_TICK` bounds starts per five-second tick and
  defaults to `16`. This is a throughput and Steam rate-protection boundary,
  not a Target Server concurrency limit.

### Disable and Delete Lifecycle

- Updating a Target Server to disabled persists that disabled state first,
  preventing further scheduling. Core then sends stop requests to all active
  or uncertain operations for that target.
- A confirmed stop marks the attempt completed and removes its matching
  reservation lease. A stop error quarantines that Agent, leaves the attempt
  and lease for recovery, and makes the endpoint return `409`.
- Deletion first persists disabled, drains operations using the same process,
  and removes the Target Server only after all stops are confirmed. A failed
  drain leaves the disabled Target Server in place and returns `409`.

## Compatibility Boundary

Existing `/v1/servers`, `/v1/agents` and `/v1/lobbies/{id}` responses remain
compatible. The new route is additive. Explicit reservation rejection and
reservation targets without RCON credentials retain their current behavior.
The pre-existing standalone Agent container remains outside Core lifecycle
operations.

## Non-goals

- Completed-attempt history API, per-target RCON administration, user-facing
  dashboard, retention policy and secret-key rotation.
- Cross-host scheduling, public Agent access, or unbounded Steam creation
  concurrency.

## Verification

Automated tests cover credential rejection/clearing, warm-up status redaction
and filtering, a batch with multiple healthy Agents, capacity/lease limits,
and disable/delete success and failed-stop behavior. Full solution test/build
and remote deployment verify the new Core image and one multi-Agent batch
when additional logged-in accounts are available.

## Draft Inputs

- **TaskIntentDraft:** make Core observable and efficient for many managed
  Agents while preserving reservation safety.
- **BaselineReadSetHint:** `CONTEXT.md`, target server API/service, scheduler,
  agent control contract, prior RCON reservation design and deployment docs.
- **ImpactStatementDraft:** changes Core API, scheduler/Agent selector and
  Target Server lifecycle only. Steam mutation remains owned by Agent
  operations; Core remains the owner of scheduling and cleanup state.
