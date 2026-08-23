using L4d2Matchmaking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class PlayerEntryEventFactoryTests
{
    [TestMethod]
    public void SuccessfulReplyCreatesOneSnapshotEvent()
    {
        var context = new EntryStatisticsContext(
            Guid.NewGuid(),
            "agent-at-start",
            " hongkong ",
            Guid.NewGuid(),
            "203.0.113.7:27015",
            "server-at-start",
            "");
        var request = new AgentOperationRequest(
            Guid.NewGuid(),
            AgentLobbyMode.Reserved,
            "127.0.0.1",
            27015)
        {
            EntryStatisticsContext = context,
        };
        var occurredAt = DateTimeOffset.Parse("2026-08-23T00:00:00+00:00");

        var entryEvent = PlayerEntryEventFactory.CreateAfterSuccessfulReply(request, 123UL, true, occurredAt);

        Assert.IsNotNull(entryEvent);
        Assert.AreEqual(request.OperationId, entryEvent.OperationId);
        Assert.AreEqual("123", entryEvent.LobbyId);
        Assert.AreEqual(PlayerEntryStatisticsContract.LobbyTypeReserved, entryEvent.LobbyType);
        Assert.AreEqual(context.AgentId, entryEvent.AgentId);
        Assert.AreEqual("33", entryEvent.DownloadRegionSnapshot);
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeVersus, entryEvent.TargetModeSnapshot);
        Assert.AreEqual(occurredAt, entryEvent.OccurredAtUtc);
    }

    [TestMethod]
    [DataRow(false)]
    public void FailedSendDoesNotCreateAnEvent(bool sent)
    {
        var request = new AgentOperationRequest(Guid.NewGuid(), AgentLobbyMode.Standard, "127.0.0.1", 27015)
        {
            EntryStatisticsContext = new EntryStatisticsContext(
                Guid.NewGuid(), "agent", null, Guid.NewGuid(), "127.0.0.1:27015", null, "coop"),
        };

        Assert.IsNull(PlayerEntryEventFactory.CreateAfterSuccessfulReply(
            request,
            123UL,
            sent,
            DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void LegacyOperationWithoutContextDoesNotCreateAnIncompleteEvent()
    {
        var request = new AgentOperationRequest(Guid.NewGuid(), AgentLobbyMode.Standard, "127.0.0.1", 27015);

        Assert.IsNull(PlayerEntryEventFactory.CreateAfterSuccessfulReply(
            request,
            123UL,
            true,
            DateTimeOffset.UtcNow));
    }
}
