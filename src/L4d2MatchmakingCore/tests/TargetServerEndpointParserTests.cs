using L4d2MatchmakingCore.Servers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class TargetServerEndpointParserTests
{
    [DataTestMethod]
    [DataRow("example.org", "example.org", (ushort)27015)]
    [DataRow("203.0.113.7:28015", "203.0.113.7", (ushort)28015)]
    [DataRow("Game-Server.example.org:27016", "game-server.example.org", (ushort)27016)]
    public void ParseAcceptsHostOrIpv4(string input, string host, ushort port)
    {
        Assert.AreEqual(new TargetServerAddress(host, port), TargetServerEndpointParser.Parse(input));
    }

    [DataTestMethod]
    [DataRow("https://example.org:27015")]
    [DataRow("[2001:db8::1]:27015")]
    [DataRow("2001:db8::1")]
    [DataRow("admin@example.org:27015")]
    [DataRow("example.org:0")]
    [DataRow("example.org:65536")]
    [DataRow("example .org:27015")]
    public void ParseRejectsUnsafeOrInvalidInputs(string input)
    {
        Assert.ThrowsException<ArgumentException>(() => TargetServerEndpointParser.Parse(input));
    }
}
