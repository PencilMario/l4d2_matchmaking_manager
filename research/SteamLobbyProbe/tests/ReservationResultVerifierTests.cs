using System.Net;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class ReservationResultVerifierTests
{
    [TestMethod]
    public void RconVerificationCanOnlyAcceptReservationUdpTimeouts()
    {
        var method = GetVerifier();
        var endpoint = new IPEndPoint(IPAddress.Loopback, 27083);
        var calls = 0;
        Func<IPEndPoint, string, ulong, bool> matchingStatus = (actualEndpoint, password, cookie) =>
        {
            calls++;
            Assert.AreEqual(endpoint, actualEndpoint);
            Assert.AreEqual("rcon-password", password);
            Assert.AreEqual(0x0186000059AD6053UL, cookie);
            return true;
        };

        Assert.IsTrue(Invoke(method, 8, endpoint, "rcon-password", matchingStatus));
        Assert.AreEqual(1, calls);
        Assert.IsFalse(Invoke(method, 8, endpoint, null, matchingStatus));
        Assert.AreEqual(1, calls);
        Assert.IsFalse(Invoke(method, 7, endpoint, "rcon-password", matchingStatus));
        Assert.AreEqual(1, calls);
    }

    private static MethodInfo GetVerifier()
    {
        var type = typeof(SteamNativeRuntime).Assembly.GetType("ReservationResultVerifier");
        Assert.IsNotNull(type, "The agent must own RCON fallback decision-making for ambiguous reservation results.");
        var method = type.GetMethod("IsAccepted", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "ReservationResultVerifier must expose IsAccepted.");
        return method;
    }

    private static bool Invoke(
        MethodInfo method,
        int reservationResult,
        IPEndPoint endpoint,
        string? password,
        Func<IPEndPoint, string, ulong, bool> verifier) =>
        (bool)(method.Invoke(
            null,
            [reservationResult, endpoint, 0x0186000059AD6053UL, password, verifier])
            ?? throw new AssertFailedException("Reservation result verification must return a Boolean."));
}
