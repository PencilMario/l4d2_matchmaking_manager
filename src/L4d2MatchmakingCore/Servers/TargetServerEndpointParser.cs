using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace L4d2MatchmakingCore.Servers;

public static class TargetServerEndpointParser
{
    public const ushort DefaultPort = 27015;

    public static TargetServerAddress Parse(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Any(char.IsWhiteSpace) ||
            endpoint.Contains("://", StringComparison.Ordinal) ||
            endpoint.IndexOfAny(['/', '?', '#', '@', '[', ']']) >= 0)
        {
            throw InvalidEndpoint();
        }

        var separator = endpoint.IndexOf(':');
        if (separator >= 0 && separator != endpoint.LastIndexOf(':'))
            throw InvalidEndpoint();

        var host = separator < 0 ? endpoint : endpoint[..separator];
        var port = DefaultPort;
        if (separator >= 0)
        {
            var portText = endpoint[(separator + 1)..];
            if (!ushort.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port == 0)
                throw InvalidEndpoint();
        }

        if (string.IsNullOrEmpty(host))
            throw InvalidEndpoint();

        if (IPAddress.TryParse(host, out var address))
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw InvalidEndpoint();
            return new TargetServerAddress(address.ToString(), port);
        }

        return new TargetServerAddress(NormalizeHostname(host), port);
    }

    private static string NormalizeHostname(string host)
    {
        string asciiHost;
        try
        {
            asciiHost = new IdnMapping().GetAscii(host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            throw InvalidEndpoint();
        }

        if (asciiHost.Length is 0 or > 253)
            throw InvalidEndpoint();

        foreach (var label in asciiHost.Split('.'))
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-' ||
                label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                throw InvalidEndpoint();
            }
        }

        return asciiHost;
    }

    private static ArgumentException InvalidEndpoint() =>
        new("invalid_target_server_endpoint", "endpoint");
}
