using L4d2Matchmaking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class SteamSessionActorTests
{
    [TestMethod]
    public async Task ReadLobbyUsesTheSameActorWhileAnOperationIsHeld()
    {
        var native = new FakeSteamNativeRuntime();
        await using var actor = new SteamSessionActor(
            native,
            new FixedCampaignSelector(CampaignProfile.Official[0]));
        var operation = new AgentOperationRequest(
            Guid.NewGuid(),
            AgentLobbyMode.Standard,
            "127.0.0.1",
            27015);

        var started = await actor.StartAsync(operation, CancellationToken.None);
        var health = actor.ObserveHealthAsync(CancellationToken.None);
        var lobby = actor.ReadLobbyAsync(109775242170052468UL, CancellationToken.None);
        await Task.WhenAll(health, lobby);

        Assert.IsFalse(started.AlreadyExists);
        Assert.AreEqual("109775242170052468", lobby.Result.LobbyId);
        Assert.IsTrue(health.Result.Ready);
        Assert.AreEqual(1, native.MaximumConcurrentCalls);
        Assert.AreEqual(1, native.CreateLobbyCalls);
        Assert.AreEqual(1, native.ReadLobbyCalls);
    }

    private sealed class FixedCampaignSelector(CampaignProfile profile) : ICampaignSelector
    {
        public CampaignProfile Select() => profile;
    }

    private sealed class FakeSteamNativeRuntime : ISteamNativeRuntime
    {
        private int _activeCalls;

        public int MaximumConcurrentCalls { get; private set; }
        public int CreateLobbyCalls { get; private set; }
        public int ReadLobbyCalls { get; private set; }

        public AgentHealthSnapshot ObserveHealth() => Track(() =>
            new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow));

        public LobbySnapshot CreateLobby(AgentOperationRequest request, CampaignProfile profile) => Track(() =>
        {
            CreateLobbyCalls++;
            return Snapshot("109775242170052468", profile.CampaignId);
        });

        public LobbySnapshot ReadLobby(ulong lobbyId) => Track(() =>
        {
            ReadLobbyCalls++;
            return Snapshot(lobbyId.ToString(), "L4D2C1");
        });

        public void LeaveLobby(ulong lobbyId) => Track(static () => { });

        public void PumpCallbacks()
        {
        }

        public void Dispose()
        {
        }

        private T Track<T>(Func<T> operation)
        {
            var active = Interlocked.Increment(ref _activeCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, active);
            try
            {
                Thread.Sleep(20);
                return operation();
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
            }
        }

        private void Track(Action operation) => Track(() =>
        {
            operation();
            return true;
        });

        private static LobbySnapshot Snapshot(string lobbyId, string campaign) => new(
            lobbyId,
            "76561198000000000",
            [new LobbyMemberSnapshot("76561198000000000", "Agent")],
            new Dictionary<string, string> { ["Game:campaign"] = campaign },
            DateTimeOffset.UtcNow);
    }
}
