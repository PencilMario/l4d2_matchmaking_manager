using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2AsfPlugin.Tests;

[TestClass]
public sealed class L4d2LobbyChatAuditTests
{
    [TestMethod]
    public void RequestAuditIdentifiesSenderLobbyAndConnectableStateWithoutPayload()
    {
        var audit = L4d2LobbyChatAudit.RequestReceived(
            76561199382197988UL,
            109775242121908184UL,
            L4d2HostSessionState.Ready);

        Assert.AreEqual(L4d2LobbyChatAuditEvent.RequestJoinData, audit.Event);
        Assert.AreEqual(76561199382197988UL, audit.SenderSteamId);
        Assert.AreEqual(109775242121908184UL, audit.LobbySteamId);
        Assert.AreEqual(L4d2HostSessionState.Ready, audit.State);
        Assert.AreEqual(0, audit.ReplyLength);
    }

    [TestMethod]
    public void ReplyAuditRecordsOnlyTheReplyLength()
    {
        var audit = L4d2LobbyChatAudit.ReplySent(
            76561199382197988UL,
            109775242121908184UL,
            L4d2HostSessionState.ReservationStatusRequired,
            631);

        Assert.AreEqual(L4d2LobbyChatAuditEvent.ReplyJoinData, audit.Event);
        Assert.AreEqual(631, audit.ReplyLength);
        Assert.IsFalse(typeof(L4d2LobbyChatAudit).GetProperties()
            .Any(property => property.PropertyType == typeof(byte[])));
    }
}
