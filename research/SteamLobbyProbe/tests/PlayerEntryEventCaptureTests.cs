using L4d2Matchmaking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class PlayerEntryEventCaptureTests
{
    [TestMethod]
    public void SuccessfulNativeSendEnqueuesOneSnapshotAndReturnsTrue()
    {
        var sink = new RecordingSink();
        var request = Request();

        var result = ReplyJoinDataSendCoordinator.SendAndCapture(
            () => true,
            request,
            123UL,
            sink,
            DateTimeOffset.Parse("2026-08-23T00:00:00Z"));

        Assert.IsTrue(result);
        Assert.AreEqual(1, sink.Events.Count);
        Assert.AreEqual("123", sink.Events[0].LobbyId);
        Assert.AreEqual(request.OperationId, sink.Events[0].OperationId);
    }

    [TestMethod]
    public void FailedNativeSendDoesNotEnqueueAnEvent()
    {
        var sink = new RecordingSink();

        var result = ReplyJoinDataSendCoordinator.SendAndCapture(
            () => false,
            Request(),
            123UL,
            sink,
            DateTimeOffset.UtcNow);

        Assert.IsFalse(result);
        Assert.AreEqual(0, sink.Events.Count);
    }

    [TestMethod]
    public void SinkFailureIsContainedAndDoesNotChangeNativeSendResult()
    {
        var sink = new ThrowingSink();
        Exception? captured = null;

        var result = ReplyJoinDataSendCoordinator.SendAndCapture(
            () => true,
            Request(),
            123UL,
            sink,
            DateTimeOffset.UtcNow,
            exception => captured = exception);

        Assert.IsTrue(result);
        Assert.IsNotNull(captured);
        Assert.AreEqual("sink_failed", captured.Message);
    }

    private static AgentOperationRequest Request() => new(
        Guid.NewGuid(),
        AgentLobbyMode.Standard,
        "127.0.0.1",
        27015)
    {
        EntryStatisticsContext = new EntryStatisticsContext(
            Guid.NewGuid(),
            "agent",
            "47",
            Guid.NewGuid(),
            "203.0.113.7:27015",
            "server",
            "coop"),
    };

    private sealed class RecordingSink : IPlayerEntryEventSink
    {
        public List<PlayerEntryEvent> Events { get; } = [];

        public bool TryEnqueue(PlayerEntryEvent entryEvent)
        {
            Events.Add(entryEvent);
            return true;
        }
    }

    private sealed class ThrowingSink : IPlayerEntryEventSink
    {
        public bool TryEnqueue(PlayerEntryEvent entryEvent) => throw new InvalidOperationException("sink_failed");
    }
}
