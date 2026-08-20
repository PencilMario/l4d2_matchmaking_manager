# Active Lobby JoinData Restoration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the Warm-up Agent behavior that publishes the active lobby's L4D2 server metadata and replies to real-client `RequestJoinData` messages.

**Architecture:** `SteamNativeRuntime` remains the canonical owner of native Steam lobby metadata, callback pumping, and outbound chat messages. A small `ActiveLobbyJoinDataResponder` owns only active-lobby eligibility and delegates protocol encoding to the existing `LobbyJoinProtocol.TryCreateRealReply`. The existing `SteamSessionActor` lifecycle and HTTP contracts remain unchanged.

**Tech Stack:** C#/.NET 10 Agent, Steamworks native manual dispatch, MSTest, existing `L4d2Protocol` binary KeyValues encoder.

**Baseline / Authority Refs:** `docs/aegis/work/2026-08-19-active-lobby-join-data/20-spec.md`, `research/L4d2Protocol/LobbyJoinProtocol.cs`, `research/L4d2Protocol/RealSessionSettings.cs`, `research/SteamLobbyProbe/Program.cs`, and the unmerged reference commit `46b5239`.

**Compatibility Boundary:** Preserve existing Agent/Core HTTP APIs, standard and reserved lobby flows, `SetLobbyGameServer`, reservation UDP/RCON behavior, non-active-lobby filtering, and the current `request.GameMode` metadata profile. The four server fields use runtime values: target IPv4/port and the newly-created Steam lobby ID.

**Verification:** A focused responder/metadata test must fail before implementation and pass after it; then run all `SteamLobbyProbe`, `L4d2Protocol`, and `L4d2LobbyAgent` tests, build the solution, and inspect the final diff/worktree state.

**Repair Track:** Restore the missing callback and metadata wiring in the canonical Agent runtime, with regression tests for the exact field values and active-lobby filter.

**Retirement Track:** Retire the unconditional callback discard only for valid chat callbacks belonging to the active lobby. Continue discarding malformed messages, non-chat callbacks, invalid protocol payloads, and requests for other lobbies. No fallback owner or second Steam API process is introduced.

---

### Task 1: Restore the Active-Lobby Reply Filter

**Files:**
- Create: `research/SteamLobbyProbe/ActiveLobbyJoinDataResponder.cs`
- Create: `research/SteamLobbyProbe/tests/ActiveLobbyJoinDataResponderTests.cs`

**Why this task exists:** The Agent currently has a protocol encoder but no active-operation owner that decides whether a received request may be answered. The filter prevents an Agent from synthesizing replies for arbitrary queryable lobbies.

**Impact / Compatibility:** This is a pure helper. It does not change the Agent HTTP contract, Steam lifecycle, or `LobbyJoinProtocol` encoding. Matching lobby ID and nonzero sender are required; all other requests return `false` with an empty reply.

- [x] **Step 1: Write the failing responder tests.** Add two MSTest methods and the request fixture below to `research/SteamLobbyProbe/tests/ActiveLobbyJoinDataResponderTests.cs`:

```csharp
using static BinaryKeyValues;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class ActiveLobbyJoinDataResponderTests
{
    private const ulong LobbyId = 109775242646646147UL;
    private const ulong OwnerSteamId = 76561199692804388UL;
    private const ulong RequesterSteamId = 76561198000000001UL;

    [TestMethod]
    public void MatchingActiveLobbyRequestCreatesRealReply()
    {
        var responder = new ActiveLobbyJoinDataResponder(LobbyId, OwnerSteamId, "106.54.197.3:24561");

        var created = responder.TryCreateReply(LobbyId, CreateRequest(), RequesterSteamId, out var reply);

        Assert.IsTrue(created);
        Assert.IsTrue(reply.Length > 4);
    }

    [TestMethod]
    public void OtherLobbyRequestIsIgnored()
    {
        var responder = new ActiveLobbyJoinDataResponder(LobbyId, OwnerSteamId, "106.54.197.3:24561");

        var created = responder.TryCreateReply(1UL, CreateRequest(), RequesterSteamId, out var reply);

        Assert.IsFalse(created);
        Assert.AreEqual(0, reply.Length);
    }

    private static byte[] CreateRequest()
    {
        var payload = Encode(Object("SysSession::RequestJoinData",
            UInt64("id", RequesterSteamId),
            Object("Settings",
                Object("Members",
                    Object("machine0",
                        UInt64("id", RequesterSteamId),
                        String("tuver", "00000000"),
                        UInt64("dlcmask", 0),
                        Object("player0",
                            UInt64("xuid", RequesterSteamId),
                            String("name", "Requester")))))));
        return [0, 0, 0, 0, .. payload];
    }
}
```

- [x] **Step 2: Run the focused test and verify the expected red failure.** Run:

```powershell
dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --filter FullyQualifiedName~ActiveLobbyJoinDataResponderTests --verbosity minimal
```

Expected result: compilation fails because `ActiveLobbyJoinDataResponder` does not yet exist; no production implementation is written before this red check.

- [x] **Step 3: Implement the minimal responder.** Create `research/SteamLobbyProbe/ActiveLobbyJoinDataResponder.cs`:

```csharp
internal sealed class ActiveLobbyJoinDataResponder
{
    private readonly ulong _ownerSteamId;
    private readonly string _connectString;

    internal ActiveLobbyJoinDataResponder(ulong lobbyId, ulong ownerSteamId, string connectString)
    {
        LobbyId = lobbyId;
        _ownerSteamId = ownerSteamId;
        _connectString = connectString;
    }

    internal ulong LobbyId { get; }

    internal bool TryCreateReply(
        ulong chatLobbyId,
        ReadOnlySpan<byte> request,
        ulong senderSteamId,
        out byte[] reply)
    {
        reply = Array.Empty<byte>();
        return chatLobbyId == LobbyId && senderSteamId != 0 &&
            LobbyJoinProtocol.TryCreateRealReply(
                request,
                _connectString,
                LobbyId,
                _ownerSteamId,
                senderSteamId,
                out reply);
    }
}
```

- [x] **Step 4: Run the focused test and verify green.** Run the same focused command. Expected result: both responder tests pass.

### Task 2: Restore Server Metadata and Steam Chat Callback Wiring

**Files:**
- Modify: `research/SteamLobbyProbe/SteamNativeRuntime.cs`
- Modify: `research/SteamLobbyProbe/tests/ActiveLobbyJoinDataResponderTests.cs`

**Why this task exists:** The current runtime writes the normal session profile but omits all four `server:*` fields and frees callback 507 without reading or answering it. The existing native API wrapper already exposes the required functions, so the canonical fix is runtime wiring only.

**Impact / Compatibility:** Both standard and reserved lobbies receive identical endpoint metadata and retain the existing native game-server binding and reservation flow. Only the active lobby gets a reply. The callback is always freed exactly once, including on malformed input or handler exceptions.

- [x] **Step 1: Add the exact metadata test.** Add this method to `ActiveLobbyJoinDataResponderTests`:

```csharp
[TestMethod]
public void ServerMetadataUsesTheActiveLobbyEndpointAndId()
{
    var metadata = SteamNativeRuntime.CreateServerMetadata(
        "106.54.197.3",
        24561,
        LobbyId);

    CollectionAssert.AreEquivalent(
        new Dictionary<string, string>
        {
            ["server:adrlocal"] = "106.54.197.3:24561",
            ["server:adronline"] = "106.54.197.3:24561",
            ["server:connectstring"] = "106.54.197.3:24561",
            ["server:reservationid"] = "109775242646646147",
        },
        metadata);
}
```

- [x] **Step 2: Run the focused test and verify the expected red failure.** Run the focused command. Expected result: compilation fails because `SteamNativeRuntime.CreateServerMetadata` does not yet exist.

- [x] **Step 3: Add the metadata helper and merge it into lobby creation.** In `SteamNativeRuntime`, add `LobbyChatMsgCallback = 507`, an `ActiveLobbyJoinDataResponder? _activeJoinDataResponder` field, and this helper:

```csharp
internal static Dictionary<string, string> CreateServerMetadata(string ipv4Address, ushort port, ulong lobbyId)
{
    if (!IPAddress.TryParse(ipv4Address, out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
        port == 0 || lobbyId == 0)
    {
        throw new ArgumentException("A nonzero IPv4 endpoint and lobby ID are required.");
    }

    var endpoint = address + ":" + port;
    return new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["server:adrlocal"] = endpoint,
        ["server:adronline"] = endpoint,
        ["server:connectstring"] = endpoint,
        ["server:reservationid"] = lobbyId.ToString(),
    };
}
```

In `CreateLobby`, preserve `request.GameMode` and merge the helper before the existing `SetLobbyData` loop:

```csharp
var metadata = RealSessionSettings.CreateLobbyMetadata(profile, request.GameMode);
foreach (var pair in CreateServerMetadata(address.ToString(), request.Port, created.LobbyId))
    metadata.Add(pair.Key, pair.Value);
```

After the existing reservation block and `ReadLobby(created.LobbyId)`, obtain the logged-on owner ID, construct the responder with the created lobby ID and target endpoint, then return the lobby. If the owner ID is zero, throw `SteamRuntimeException("steam_not_logged_on")`.

- [x] **Step 4: Wire active-lobby lifecycle cleanup.** In `LeaveLobby`, clear `_activeJoinDataResponder` when its lobby ID matches the lobby being left, then call the existing native leave function. In `Dispose`, set the field to `null` in the `finally` block. Keep the existing shutdown and native library cleanup unchanged.

- [x] **Step 5: Handle callback 507 in `PumpCallbacks`.** Replace the unconditional callback free with a `try/finally` that calls `HandleLobbyChatMessage` for callback 507 and always invokes `ManualDispatchFreeLastCallback` once. Add this handler:

```csharp
private void HandleLobbyChatMessage(Program.CallbackMsg callback)
{
    if (_activeJoinDataResponder is null || callback.Param == 0 || callback.ParamSize < 24)
        return;

    var raw = new byte[Math.Clamp(callback.ParamSize, 0, 64)];
    Marshal.Copy(callback.Param, raw, 0, raw.Length);
    var lobbyId = BitConverter.ToUInt64(raw, 0);
    var chatId = BitConverter.ToInt32(raw, 20);
    var buffer = Marshal.AllocHGlobal(4096);
    try
    {
        var size = _api!.GetLobbyChatEntry(
            _matchmaking,
            lobbyId,
            chatId,
            out var senderSteamId,
            buffer,
            4096,
            out var entryType);
        if (size <= 0 || entryType != 1)
            return;

        var message = new byte[Math.Min(size, 4096)];
        Marshal.Copy(buffer, message, 0, message.Length);
        if (_activeJoinDataResponder.TryCreateReply(lobbyId, message, senderSteamId, out var reply) &&
            reply.Length > 0)
        {
            var sent = _api.SendLobbyChatMsg(_matchmaking, lobbyId, reply);
            Console.WriteLine($"ReplyJoinData lobby_id={lobbyId} recipient={senderSteamId} size={reply.Length} sent={sent}");
        }
    }
    finally
    {
        Marshal.FreeHGlobal(buffer);
    }
}
```

- [x] **Step 6: Run the focused test and verify green.** Run the focused command. Expected result: all three tests pass.

### Task 3: Regression Verification and Handoff

**Files:**
- Modify: `docs/aegis/work/2026-08-21-active-lobby-join-data/50-evidence.md`

**Why this task exists:** The fix crosses the protocol library, Steam probe runtime, and production Agent project reference. Related tests and a solution build must prove that the restored path did not break existing flows.

- [x] **Step 1: Run all related tests.** Run:

```powershell
dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --verbosity minimal
dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --verbosity minimal
dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --verbosity minimal
```

Expected result: exit code 0 and zero failed tests.

- [x] **Step 2: Build the solution and Agent publish target.** Run:

```powershell
dotnet build L4d2MatchmakingManager.sln --no-restore
dotnet publish src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -c Release -r linux-x64 --self-contained false --no-restore
```

Expected result: both commands exit 0.

- [x] **Step 3: Record evidence and inspect the final diff.** Record test/build results and residual live-runtime risk in `50-evidence.md`; run `git diff --check`, `git status --short`, and `git diff --stat`. Do not claim real-client success without a live Agent callback log and actual L4D2 connection evidence.

- [ ] **Step 4: Commit the isolated branch.** Commit only the plan/evidence and implementation files for this repair, then report the commit and the clean/dirty state of all worktrees. Do not merge or delete any other worktree branch without explicit user instruction.
