using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamKit2;

namespace L4d2AsfPlugin.Tests;

[TestClass]
public sealed class L4d2LobbyChatMessageTests
{
    [TestMethod]
    public void OutboundChatPreservesBinaryPayloadWithoutTerminator()
    {
        var lobbyId = new SteamID(109775242121908184);
        var senderId = new SteamID(76561199012457364);
        var reply = Convert.FromHexString("000008C30053797373090B");

        var message = L4d2LobbyChatMessage.CreateOutbound(lobbyId, senderId, reply);
        var payload = L4d2LobbyChatMessage.ReadPayload(message);

        CollectionAssert.AreEqual(reply, payload);
        Assert.AreEqual(reply.Length, payload.Length);
        Assert.AreEqual(EChatEntryType.ChatMsg, message.Body.ChatMsgType);
        Assert.AreEqual(lobbyId, message.Body.SteamIdChatRoom);
        Assert.AreEqual(senderId, message.Body.SteamIdChatter);
    }
}
