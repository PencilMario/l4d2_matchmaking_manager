using L4d2Matchmaking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class SteamSessionActorQueryTests
{
    private const ulong QueryLobbyId = 109775242170052468UL;
    private const ulong ManagedLobbyId = 109775242170052469UL;
    private const ulong PlayerSteamId = 76561198000000001UL;

    [TestMethod]
    public async Task QueryTemporarilyJoinsAndKeepsTheQueryAccountInMembers()
    {
        var native = new QueryRuntime();
        await using var actor = CreateActor(native);

        var snapshot = await actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None);

        Assert.AreEqual(LobbyMemberDataStatus.Complete, snapshot.MemberDataStatus);
        CollectionAssert.AreEqual(new[] { native.CurrentSteamId.ToString(), PlayerSteamId.ToString() }, snapshot.Members.Select(member => member.SteamId).ToArray());
        Assert.AreEqual(1, native.JoinCalls);
        Assert.AreEqual(1, native.LeaveCalls);
        Assert.IsFalse(native.IsCurrentUserLobbyMember(QueryLobbyId, native.CurrentSteamId));
    }

    [TestMethod]
    public async Task QueryNeverTemporarilyJoinsWhileHoldingAReservation()
    {
        var native = new QueryRuntime();
        await using var actor = CreateActor(native);
        await actor.StartAsync(Operation(AgentLobbyMode.Reserved), CancellationToken.None);

        var snapshot = await actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None);

        Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyAgentStateChanged, snapshot.MemberDataStatus);
        Assert.AreEqual(0, snapshot.Members.Count);
        Assert.AreEqual(0, native.JoinCalls);
    }

    [TestMethod]
    public async Task QueryKeepsTheHeldLobbyAgentWhenTheFreshReadHasNoMembers()
    {
        var native = new QueryRuntime { ReadHeldLobbyWithoutMembers = true };
        await using var actor = CreateActor(native);
        await actor.StartAsync(Operation(AgentLobbyMode.Standard), CancellationToken.None);

        var snapshot = await actor.QueryLobbyAsync(ManagedLobbyId, includeMembers: true, CancellationToken.None);

        CollectionAssert.Contains(snapshot.Members.Select(member => member.SteamId).ToArray(), native.CurrentSteamId.ToString());
    }

    [TestMethod]
    public async Task QueryReturnsMetadataOnlyWhenCoreHasNoMembershipCandidate()
    {
        var native = new QueryRuntime();
        await using var actor = CreateActor(native);

        var snapshot = await actor.QueryLobbyAsync(QueryLobbyId, includeMembers: false, CancellationToken.None);

        Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyNoQueryAgent, snapshot.MemberDataStatus);
        Assert.AreEqual(0, snapshot.Members.Count);
        Assert.AreEqual(0, native.JoinCalls);
    }

    [TestMethod]
    public async Task QueryReturnsConfirmedMetadataWhenSteamDeniesTheTemporaryJoin()
    {
        var native = new QueryRuntime { JoinResult = NativeLobbyJoinResult.Denied };
        await using var actor = CreateActor(native);

        var snapshot = await actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None);

        Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyJoinDenied, snapshot.MemberDataStatus);
        Assert.AreEqual(0, snapshot.Members.Count);
        Assert.AreEqual(0, native.LeaveCalls);
    }

    [TestMethod]
    public async Task QueryReturnsConfirmedMetadataWhenSteamTimesOutJoining()
    {
        var native = new QueryRuntime { JoinResult = NativeLobbyJoinResult.Timeout };
        await using var actor = CreateActor(native);

        var snapshot = await actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None);

        Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyJoinTimeout, snapshot.MemberDataStatus);
        Assert.AreEqual(0, snapshot.Members.Count);
        Assert.AreEqual(0, native.LeaveCalls);
    }

    [TestMethod]
    public async Task QueryLeavesTheTemporaryLobbyWhenReadingMembersFails()
    {
        var native = new QueryRuntime { ThrowAfterJoining = true };
        await using var actor = CreateActor(native);

        await Assert.ThrowsExceptionAsync<SteamRuntimeException>(
            () => actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None));

        Assert.AreEqual(1, native.LeaveCalls);
        Assert.IsFalse(native.IsCurrentUserLobbyMember(QueryLobbyId, native.CurrentSteamId));
    }

    [TestMethod]
    public async Task QueryFailsTheStandardOperationWhenLeavingDoesNotRestoreItsLobby()
    {
        var native = new QueryRuntime { PreserveOriginalLobby = false };
        await using var actor = CreateActor(native);
        var operation = Operation(AgentLobbyMode.Standard);
        await actor.StartAsync(operation, CancellationToken.None);

        var exception = await Assert.ThrowsExceptionAsync<SteamRuntimeException>(
            () => actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None));
        var after = await actor.GetOperationAsync(operation.OperationId, CancellationToken.None);

        Assert.AreEqual("lobby_operation_preservation_failed", exception.Code);
        Assert.IsNotNull(after);
        Assert.AreEqual("failed", after.State);
        Assert.AreEqual(1, native.LeaveCalls);
    }

    [TestMethod]
    public async Task QueryFailsTheStandardOperationWhenLeavingTheTemporaryLobbyThrows()
    {
        var native = new QueryRuntime { ThrowOnLeave = true };
        await using var actor = CreateActor(native);
        var operation = Operation(AgentLobbyMode.Standard);
        await actor.StartAsync(operation, CancellationToken.None);

        var exception = await Assert.ThrowsExceptionAsync<SteamRuntimeException>(
            () => actor.QueryLobbyAsync(QueryLobbyId, includeMembers: true, CancellationToken.None));
        var after = await actor.GetOperationAsync(operation.OperationId, CancellationToken.None);

        Assert.AreEqual("lobby_operation_preservation_failed", exception.Code);
        Assert.IsNotNull(after);
        Assert.AreEqual("failed", after.State);
    }

    private static SteamSessionActor CreateActor(QueryRuntime runtime) =>
        new(runtime, new FixedCampaignSelector(CampaignProfile.Official[0]));

    private static AgentOperationRequest Operation(AgentLobbyMode mode) => new(
        Guid.NewGuid(),
        mode,
        "127.0.0.1",
        27015);

    private sealed class FixedCampaignSelector(CampaignProfile profile) : ICampaignSelector
    {
        public CampaignProfile Select() => profile;
    }

    private sealed class QueryRuntime : ISteamNativeRuntime
    {
        private const ulong ManagedLobbyId = 109775242170052469UL;
        private readonly HashSet<ulong> _joined = [];

        public ulong CurrentSteamId { get; } = 76561198000000000UL;
        public int JoinCalls { get; private set; }
        public int LeaveCalls { get; private set; }
        public NativeLobbyJoinResult JoinResult { get; set; } = NativeLobbyJoinResult.Success;
        public bool ThrowAfterJoining { get; set; }
        public bool ThrowOnLeave { get; set; }
        public bool PreserveOriginalLobby { get; set; } = true;
        public bool ReadHeldLobbyWithoutMembers { get; set; }

        public AgentHealthSnapshot ObserveHealth() => new(true, null, DateTimeOffset.UtcNow);

        public LobbySnapshot CreateLobby(AgentOperationRequest request, CampaignProfile profile)
        {
            _joined.Add(ManagedLobbyId);
            return Snapshot(ManagedLobbyId, profile.CampaignId, includeSelf: true);
        }

        public LobbySnapshot ReadLobby(ulong lobbyId)
        {
            if (ThrowAfterJoining && _joined.Contains(lobbyId))
                throw new SteamRuntimeException("lobby_data_unavailable");
            if (ReadHeldLobbyWithoutMembers && lobbyId == ManagedLobbyId)
                return Snapshot(lobbyId, "L4D2C1", includeSelf: false);
            return Snapshot(lobbyId, "L4D2C1", _joined.Contains(lobbyId));
        }

        public NativeLobbyJoinResult JoinLobby(ulong lobbyId)
        {
            JoinCalls++;
            if (JoinResult == NativeLobbyJoinResult.Success)
                _joined.Add(lobbyId);
            return JoinResult;
        }

        public ulong GetCurrentSteamId() => CurrentSteamId;

        public bool IsCurrentUserLobbyMember(ulong lobbyId, ulong steamId) =>
            steamId == CurrentSteamId && _joined.Contains(lobbyId);

        public void LeaveLobby(ulong lobbyId)
        {
            LeaveCalls++;
            if (ThrowOnLeave)
                throw new InvalidOperationException("leave_failed");
            _joined.Remove(lobbyId);
            if (!PreserveOriginalLobby)
                _joined.Remove(ManagedLobbyId);
        }

        public void PumpCallbacks()
        {
        }

        public void Dispose()
        {
        }

        private LobbySnapshot Snapshot(ulong lobbyId, string campaign, bool includeSelf) => new(
            lobbyId.ToString(),
            CurrentSteamId.ToString(),
            includeSelf
                ? [new LobbyMemberSnapshot(CurrentSteamId.ToString(), "Agent"), new LobbyMemberSnapshot(PlayerSteamId.ToString(), "Player")]
                : [],
            new Dictionary<string, string> { ["Game:campaign"] = campaign },
            DateTimeOffset.UtcNow);
    }
}
