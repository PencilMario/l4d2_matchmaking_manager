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
