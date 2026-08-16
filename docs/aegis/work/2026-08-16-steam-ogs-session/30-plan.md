# Steam OGS App Session Experiment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Test whether the SteamKit 3.x legacy `ClientOGSBeginSession` message supplies the app-session state that a real L4D2 client requires before joining an ASF-owned lobby.

**Architecture:** Add a temporary, opt-in SteamKit handler that sends `ClientOGSBeginSession` for AppID 550, records its response `sessionId`, and keeps the session open while the existing lobby/reservation flow runs. The existing CreateLobby, metadata, reservation, and JoinData paths remain unchanged. The experiment is removed if a real L4D2 URI still produces no lobby membership or `RequestJoinData`.

**Tech Stack:** .NET 10, ASF 6.3.8.4 plugin API, SteamKit2 3.4.0, MSTest, Docker, remote Edge L4D2 client.

**Baseline / Authority Refs:** `docs/aegis/work/2026-08-15-l4d2-asf-reservation/50-evidence.md`, SteamKit source `SteamLanguageInternal.cs` (`MsgClientOGSBeginSession`), `research/L4d2AsfPlugin/L4d2LobbyProbePlugin.cs`, and existing `SteamLobbyProbe` live-join evidence.

**Compatibility Boundary:** The normal plugin configuration and lifecycle remain byte-for-byte unchanged when the new flag is absent or false. No credentials, server settings, metadata, reservation encoding, or permanent Edge tasks are changed. The temporary OGS session must be ended or allowed to close during cleanup.

**Verification:** First prove exact legacy message fields and response handling with unit tests, then build and run one live URI attempt within the reservation lifetime. Success requires Edge to appear as a valid lobby member and ASF to log `RequestJoinData`; only those observations justify keeping the branch.

**Facts / assumptions / unknowns:**

- Fact: SteamKit 3.x exposes `MsgClientOGSBeginSession` with `AccountType`, `AccountId`, `AppId`, and `TimeStarted`, plus a response `SessionId`.
- Fact: ASF currently sends no OGS begin-session message.
- Assumption: OGS is the “app-session” the user referred to; this is the hypothesis under test, not a protocol conclusion.
- Unknown: whether L4D2 matchmaking consults OGS state, whether the message is accepted for AppID 550, and whether a returned session ID must be embedded in another message.

### Task 1: Add the Temporary OGS Transport

**Files:**
- Create: `research/L4d2AsfPlugin/L4d2OgsSession.cs`
- Create: `research/L4d2AsfPlugin/L4d2OgsSessionHandler.cs`
- Create: `research/L4d2AsfPlugin/tests/L4d2OgsSessionTests.cs`
- Modify: `research/L4d2AsfPlugin/tests/L4d2AsfPlugin.Tests.csproj`

**Repair Track:** The candidate owner is a dedicated SteamKit handler for the legacy OGS message; it must not be mixed into reservation or lobby metadata code.

**Retirement Track:** This code is experimental and has no production default. Delete the files and project links if the live test is negative.

- [x] Write tests for `Individual` account type, AppID 550, nonzero start time, and response `sessionId` capture.
- [x] Run the target tests and verify they fail because the transport types do not exist.
- [x] Implement the smallest handler that sends `ClientMsg<MsgClientOGSBeginSession>`, handles only `ClientOGSBeginSessionResponse`, and exposes the active session ID.
- [x] Run the target tests and full plugin tests.

### Task 2: Gate the Lifecycle

**Files:**
- Modify: `research/L4d2AsfPlugin/L4d2PluginConfiguration.cs`
- Modify: `research/L4d2AsfPlugin/L4d2LobbyProbePlugin.cs`
- Modify: `research/L4d2AsfPlugin/tests/L4d2PluginConfigurationTests.cs`

**Repair Track:** Add one opt-in flag and send OGS before CreateLobby, so the experiment changes only one protocol variable.

**Retirement Track:** The flag and lifecycle branch are temporary. Remove them with Task 1 if no L4D2 join evidence appears.

- [x] Add a default-off configuration test and verify RED.
- [x] Add handler registration by bot name because Steam handlers initialize before login and SteamID is zero at that point.
- [x] Send OGS BeginSession before the existing CreateLobby call and log only result/session/lobby identifiers.
- [x] Run full tests and Release build with zero warnings and errors.

### Task 3: Run the Bounded Live Test and Restore

**Files:**
- Temporary remote ASF plugin DLL and JSON only.
- Modify: `docs/aegis/work/2026-08-15-l4d2-asf-reservation/50-evidence.md`

**Verification:** Back up remote files without printing secrets, deploy only the opt-in build, restart ASF, reserve one configured test server, launch Edge in its existing interactive session, inspect valid lobby membership, collect ASF `join_data`, then restore the exact original DLL/config and remove temporary scheduled tasks.

- [x] Record OGS send order, response result, session ID presence, lobby ID, reservation result, and JoinData events.
- [x] Treat missing Edge membership or JoinData as a negative result even if OGS returns `EResult.OK`.
- [x] Restore remote state and verify the original plugin hash/config behavior.
- [x] Append evidence and delete the temporary implementation after the hypothesis was disproven.
