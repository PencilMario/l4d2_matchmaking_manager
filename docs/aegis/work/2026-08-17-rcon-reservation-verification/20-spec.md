# RCON Reservation Verification Design

## Goal

Allow a reservation-capable Target Server to carry an optional Source RCON
credential so that an Agent can verify an ambiguous UDP reservation timeout
against the server's authoritative `status` output.

## Evidence and Root Cause

On 2026-08-17, the Agent created a lobby for `202.105.108.88:27083`, sent a
reservation request, and received no UDP reservation reply within five
seconds. The Agent treated that as `reservation_failed` and left the lobby.
RCON `status` immediately reported `reserved <the created lobby cookie>`.
Subsequent retries were rejected while that lease remained active. Therefore,
the UDP reply is not authoritative for this server; `status` is.

## Scope

- Target Server create/update accepts an optional `rconPassword`.
- Core encrypts the password before persistence using a 32-byte AES-GCM key
  read from `CORE_RCON_ENCRYPTION_KEY_FILE_HOST`; API responses expose only
  `hasRconCredentials`.
- Core decrypts only while constructing an internal Agent operation request.
- Reserved Agent operations use Source RCON `status` after a UDP timeout and
  succeed only when `reserved <current lobby cookie in lowercase hex>` is
  present.
- A configured RCON port is unnecessary in this slice: Source RCON uses the
  Target Server's game port.

## Security Boundary

The database contains versioned AES-GCM ciphertext, nonce and authentication
tag, never plaintext. The encryption key is a read-only deployment secret and
is not stored in PostgreSQL or returned by any API. The plaintext crosses only
the existing private Core-to-Agent Docker network request and remains in the
Agent process for the verification call. It must never appear in logs, audit
JSON, operation snapshots, exceptions, or documentation examples.

`rconPassword` can only be supplied by authenticated create/update requests.
An update with `null` removes existing credentials; omitted is not possible in
the existing full-replacement update shape. Existing clients retain their
behavior by passing `null`.

## Runtime Behavior

For a reserved operation without credentials, the existing UDP behavior stays
unchanged. With credentials:

1. Create the Steam lobby, write metadata, set the game server, and send the
   reservation UDP packet.
2. An explicit negative reply or host-version mismatch fails and leaves the
   lobby.
3. A positive reply succeeds without RCON.
4. A UDP timeout opens a short Source RCON connection to the target endpoint,
   authenticates, executes `status`, and requires a case-insensitive exact
   `reserved <lobby CSteamID hex>` token.
5. A matching status succeeds and keeps the lobby. Authentication failures,
   command timeouts, malformed output, no matching cookie, or network errors
   fail and leave the lobby.

This preserves the Agent as the owner of the lobby cookie and reservation
verification. Core owns encryption and credentials, while the existing Agent
operation protocol remains the only lobby mutation path.

## Compatibility and Non-Goals

The server endpoint, existing defaults, public responses, Agent health routes,
and non-reservation flows remain compatible. No RCON password is exposed
through `GET /v1/servers`, operation snapshots, audit records or Agent logs.
This slice does not add RCON command administration, a separate RCON port,
or post-reservation player joining automation.

## Verification

Automated tests cover encryption round trips and invalid keys, endpoint
redaction and credential replacement, operation request propagation, Source
RCON framing/status parsing, timeout verification success/failure, and the
unchanged no-credential path. Live validation on `27083` uses its configured
RCON credential to prove a UDP timeout becomes an active, held reservation
lobby whose cookie matches `status`.
