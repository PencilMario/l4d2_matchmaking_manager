# RCON Reservation Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify ambiguous L4D2 reservation UDP timeouts through encrypted per-server RCON credentials.

**Architecture:** Core encrypts the optional Target Server credential at rest and passes it only in the private Agent operation request. The Agent owns Source RCON status verification because it owns the created lobby cookie; it keeps the lobby only when `status` confirms that exact cookie.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core/PostgreSQL, AES-GCM, Source RCON TCP protocol, MSTest.

**Baseline / Authority Refs:** `CONTEXT.md`, `docs/aegis/work/2026-08-17-core-controller/20-spec.md`, `research/SteamLobbyProbe/README.md`, and this task's `20-spec.md`.

**Compatibility Boundary:** Existing Target Server defaults and public read responses stay usable; passwords never leave authenticated write input, encrypted persistence, or private Core-to-Agent transport. No-RCON behavior is unchanged.

**Verification:** Focused MSTest projects, full solution build/test, image rebuild, and live Core/Agent/RCON verification on `202.105.108.88:27083`.

---

### Task 1: Core Secret Persistence and API Redaction

**Files:**
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`, `MatchmakingDbContext.cs`, and a new EF migration.
- Modify: `src/L4d2MatchmakingCore/Servers/TargetServerDtos.cs`, `TargetServerService.cs`, endpoint tests.
- Create: focused Core encryption service and tests.

**Repair Track:** Replace plaintext-free but unverifiable Target Server configuration with encrypted, optional credentials owned by Core. Public reads remain redacted.

**Retirement Track:** The old no-RCON configuration remains valid when no password is supplied; it is not deleted because public and legacy servers may not expose RCON.

- [ ] Add failing tests proving write input is redacted, a password round-trips only through encryption, and `null` removes a credential.
- [ ] Add AES-GCM key-file options, encrypted database column and migration.
- [ ] Add `rconPassword` write input and `hasRconCredentials` read output; never serialize ciphertext.
- [ ] Run focused Core tests.

### Task 2: Private Operation Contract and Source RCON Verifier

**Files:**
- Modify: `src/L4d2Matchmaking.Contracts/AgentContracts.cs`, Core Agent client tests, Agent endpoint tests.
- Modify: `research/SteamLobbyProbe/SteamNativeRuntime.cs` and reservation support.
- Create: focused Source RCON client/parser tests.

**Repair Track:** Make UDP timeout an ambiguous reservation outcome, not an unconditional failure, when a credential permits server-side verification.

**Retirement Track:** Explicit negative UDP replies and no-credential timeouts retain their existing failure behavior.

- [ ] Add failing contract tests for optional private RCON password propagation and ensure snapshots omit it.
- [ ] Add deterministic Source RCON authentication, framing, timeout and `reserved <cookie>` parsing tests.
- [ ] Implement verifier injection and require matching RCON status only for UDP timeout with a credential.
- [ ] Run Agent and protocol tests.

### Task 3: Scheduler Integration, Deployment and Evidence

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs` and scheduler tests.
- Modify: deployment env examples, compose files and API/deployment documentation.
- Create: task evidence record.

**Repair Track:** Core decrypts credentials only at the Agent request boundary; it does not log or persist them in operations/audits.

**Retirement Track:** The previous fast retry behavior remains only for legacy targets without RCON; RCON-equipped targets use verified agent success.

- [ ] Add failing scheduler test asserting the decrypted password is passed only for the selected reservation target.
- [ ] Wire the service through dependency injection and document key-file provisioning.
- [ ] Run the complete test/build suite, rebuild remote images, deploy, and prove the `27083` cookie using RCON `status`.
