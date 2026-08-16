using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamKit2;

namespace L4d2AsfPlugin.Tests;

[TestClass]
public sealed class L4d2HostSessionTests
{
    private const ulong LobbyId = 109775242121908184;
    private const ulong OwnerSteamId = 76561199012457364;
    private const ulong RequesterSteamId = 76561199382197988;
    private const string Endpoint = "106.54.197.3:24561";
    private const string RequestHex =
        "000008C30053797353657373696F6E3A3A526571756573744A6F696E4461746100076964000110000154C0F6E40053657474696E677300004D656D6265727300026E756D4D616368696E65730000000001026E756D506C61796572730000000001026E756D536C6F74730000000001006D616368696E653000076964000110000154C0F6E4026E756D506C6179657273000000000107646C636D61736B000000000000000000017475766572003030303030303030000270696E67000000000000706C6179657230000778756964000110000154C0F6E4016E616D65005AE38082000067616D650002736B5F76657273757300000000090B0B0B0B0B0B0B";

    [TestMethod]
    public void ReadySessionCreatesRealReplyForMatchingLobbyAndRequester()
    {
        var session = CreateSession(L4d2HostSessionState.Ready);

        var created = session.TryCreateReply(
            new SteamID(LobbyId),
            RequesterSteamId,
            Convert.FromHexString(RequestHex),
            out var reply);

        Assert.IsTrue(created);
        CollectionAssert.AreEqual(Convert.FromHexString("000008C3"), reply[..4]);
        Assert.IsTrue(ContainsSequence(reply, Encoding.UTF8.GetBytes(Endpoint)));
        Assert.IsTrue(ContainsSequence(reply, Encoding.UTF8.GetBytes("connectstring")));
    }

    [TestMethod]
    public void SessionRejectsRequestFromAnotherLobby()
    {
        var session = CreateSession(L4d2HostSessionState.Ready);

        var created = session.TryCreateReply(
            new SteamID(LobbyId + 1),
            RequesterSteamId,
            Convert.FromHexString(RequestHex),
            out var reply);

        Assert.IsFalse(created);
        Assert.AreEqual(0, reply.Length);
    }

    [TestMethod]
    public void SessionRejectsSenderDifferentFromRequestIdentity()
    {
        var session = CreateSession(L4d2HostSessionState.Ready);

        var created = session.TryCreateReply(
            new SteamID(LobbyId),
            RequesterSteamId + 1,
            Convert.FromHexString(RequestHex),
            out var reply);

        Assert.IsFalse(created);
        Assert.AreEqual(0, reply.Length);
    }

    [DataTestMethod]
    [DataRow((int)L4d2HostSessionState.Creating)]
    [DataRow((int)L4d2HostSessionState.ReservationPending)]
    [DataRow((int)L4d2HostSessionState.Failed)]
    public void SessionDoesNotReplyBeforeItIsConnectable(int stateValue)
    {
        var session = CreateSession((L4d2HostSessionState)stateValue);

        var created = session.TryCreateReply(
            new SteamID(LobbyId),
            RequesterSteamId,
            Convert.FromHexString(RequestHex),
            out var reply);

        Assert.IsFalse(created);
        Assert.AreEqual(0, reply.Length);
    }

    private static L4d2HostSession CreateSession(L4d2HostSessionState state) => new(
        new SteamID(LobbyId),
        OwnerSteamId,
        new IPEndPoint(IPAddress.Parse("106.54.197.3"), 24561),
        state);

    private static bool ContainsSequence(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> sequence)
    {
        for (var index = 0; index <= payload.Length - sequence.Length; index++)
        {
            if (payload.Slice(index, sequence.Length).SequenceEqual(sequence))
                return true;
        }

        return false;
    }
}
