using System.Security.Cryptography;
using System.Text;
using L4d2MatchmakingCore.Data;

namespace L4d2MatchmakingCore.Auth;

public static class AgentReportingTokenService
{
    public static AgentReportingToken Create(Guid agentId)
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var token = agentId.ToString("N") + "." + secret;
        return new AgentReportingToken(token, Hash(token));
    }

    public static bool TryParse(string token, out Guid agentId)
    {
        agentId = Guid.Empty;
        var separator = token.IndexOf('.');
        return separator == 32 &&
            Guid.TryParseExact(token[..separator], "N", out agentId) &&
            token.Length > separator + 1;
    }

    public static bool Matches(string token, WarmupAgent agent)
    {
        if (string.IsNullOrWhiteSpace(agent.EntryReportingTokenHash))
            return false;
        try
        {
            var actual = Convert.FromHexString(Hash(token));
            var expected = Convert.FromHexString(agent.EntryReportingTokenHash);
            return expected.Length == 32 && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static WarmupAgent BuildAgent(Guid agentId, string token) => new()
    {
        Id = agentId,
        Name = "agent-" + agentId.ToString("N"),
        SteamDataVolumeName = "steam-" + agentId.ToString("N"),
        AccountConfigVolumeName = "config-" + agentId.ToString("N"),
        NoVncPort = 18083,
        Status = "running",
        EntryReportingTokenHash = Hash(token),
    };

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed record AgentReportingToken(string Token, string Hash);
