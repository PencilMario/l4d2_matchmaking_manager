using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SteamKit2;

namespace L4d2AsfPlugin;

internal sealed record L4d2PluginConfiguration(
    IReadOnlySet<string> EnabledBots,
    IPEndPoint? Endpoint,
    bool ReservationEnabled,
    ELobbyType LobbyType,
    int MaxMembers,
    TimeSpan ReservationTimeout,
    int HostVersion)
{
    private const int DefaultHostVersion = 2243;
    private static readonly TimeSpan DefaultReservationTimeout = TimeSpan.FromSeconds(5);

    internal bool IsEnabledFor(string botName)
    {
        ArgumentException.ThrowIfNullOrEmpty(botName);
        return EnabledBots.Contains(botName);
    }

    internal static bool TryLoad(
        string directory,
        out L4d2PluginConfiguration configuration,
        out string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        var configurationPath = Path.Combine(directory, "L4d2AsfPlugin.json");
        if (!File.Exists(configurationPath))
        {
            configuration = Disabled();
            error = string.Empty;
            return true;
        }

        try
        {
            return TryParse(File.ReadAllText(configurationPath), out configuration, out error);
        }
        catch (IOException exception)
        {
            configuration = Disabled();
            error = $"Unable to read plugin configuration: {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            configuration = Disabled();
            error = $"Unable to read plugin configuration: {exception.Message}";
            return false;
        }
    }

    internal static bool TryParse(
        string json,
        out L4d2PluginConfiguration configuration,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(json);

        ConfigurationDocument? document;
        try
        {
            document = ParseConfigurationDocument(json);
        }
        catch (JsonException exception)
        {
            configuration = Disabled();
            error = $"Invalid plugin JSON: {exception.Message}";
            return false;
        }

        if (document == null)
        {
            configuration = Disabled();
            error = "Plugin configuration must be a JSON object.";
            return false;
        }

        var enabledBots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.EnabledBots != null)
        {
            foreach (var botName in document.EnabledBots)
            {
                if (!string.IsNullOrWhiteSpace(botName))
                    enabledBots.Add(botName);
            }
        }

        if (enabledBots.Count == 0)
        {
            configuration = Disabled();
            error = string.Empty;
            return true;
        }

        if (!TryParseEndpoint(document.Endpoint, out var endpoint))
        {
            configuration = Disabled();
            error = "Endpoint must be an IPv4 address in the form ip:port.";
            return false;
        }

        var maxMembers = document.MaxMembers ?? 8;
        if (maxMembers is < 1 or > 8)
        {
            configuration = Disabled();
            error = "MaxMembers must be between 1 and 8.";
            return false;
        }

        var timeoutMilliseconds = document.ReservationTimeoutMilliseconds ?? (int)DefaultReservationTimeout.TotalMilliseconds;
        if (timeoutMilliseconds <= 0)
        {
            configuration = Disabled();
            error = "ReservationTimeoutMilliseconds must be positive.";
            return false;
        }

        var hostVersion = document.HostVersion ?? DefaultHostVersion;
        if (hostVersion <= 0)
        {
            configuration = Disabled();
            error = "HostVersion must be positive.";
            return false;
        }

        if (!Enum.TryParse<ELobbyType>(document.LobbyType ?? nameof(ELobbyType.Public), true, out var lobbyType)
            || !Enum.IsDefined(lobbyType))
        {
            configuration = Disabled();
            error = "LobbyType must be a SteamKit2 ELobbyType value.";
            return false;
        }

        configuration = new L4d2PluginConfiguration(
            enabledBots,
            endpoint,
            document.ReservationEnabled,
            lobbyType,
            maxMembers,
            TimeSpan.FromMilliseconds(timeoutMilliseconds),
            hostVersion);
        error = string.Empty;
        return true;
    }

    private static L4d2PluginConfiguration Disabled() => new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        null,
        false,
        ELobbyType.Public,
        8,
        DefaultReservationTimeout,
        DefaultHostVersion);

    private static ConfigurationDocument? ParseConfigurationDocument(string json)
    {
        using var jsonDocument = JsonDocument.Parse(json);
        if (jsonDocument.RootElement.ValueKind == JsonValueKind.Null)
            return null;
        if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Plugin configuration must be a JSON object.");

        var document = new ConfigurationDocument();
        foreach (var property in jsonDocument.RootElement.EnumerateObject())
        {
            if (property.Name.Equals("EnabledBots", StringComparison.OrdinalIgnoreCase))
                document.EnabledBots = ReadStringArray(property.Value, property.Name);
            else if (property.Name.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
                document.Endpoint = ReadNullableString(property.Value, property.Name);
            else if (property.Name.Equals("ReservationEnabled", StringComparison.OrdinalIgnoreCase))
                document.ReservationEnabled = ReadBoolean(property.Value, property.Name);
            else if (property.Name.Equals("LobbyType", StringComparison.OrdinalIgnoreCase))
                document.LobbyType = ReadNullableString(property.Value, property.Name);
            else if (property.Name.Equals("MaxMembers", StringComparison.OrdinalIgnoreCase))
                document.MaxMembers = ReadNullableInt32(property.Value, property.Name);
            else if (property.Name.Equals("ReservationTimeoutMilliseconds", StringComparison.OrdinalIgnoreCase))
                document.ReservationTimeoutMilliseconds = ReadNullableInt32(property.Value, property.Name);
            else if (property.Name.Equals("HostVersion", StringComparison.OrdinalIgnoreCase))
                document.HostVersion = ReadNullableInt32(property.Value, property.Name);
        }

        return document;
    }

    private static string?[]? ReadStringArray(JsonElement value, string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Array)
            throw new JsonException($"{propertyName} must be an array or null.");

        var entries = new List<string?>();
        foreach (var entry in value.EnumerateArray())
            entries.Add(ReadNullableString(entry, propertyName));
        return entries.ToArray();
    }

    private static string? ReadNullableString(JsonElement value, string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new JsonException($"{propertyName} must be a string or null.");
        return value.GetString();
    }

    private static bool ReadBoolean(JsonElement value, string propertyName) => value.ValueKind switch
    {
        JsonValueKind.Null => false,
        JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        _ => throw new JsonException($"{propertyName} must be a boolean or null.")
    };

    private static int? ReadNullableInt32(JsonElement value, string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new JsonException($"{propertyName} must be a 32-bit integer or null.");
        return number;
    }

    private static bool TryParseEndpoint(string? value, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var separator = value.LastIndexOf(':');
        if (separator <= 0
            || !IPAddress.TryParse(value[..separator], out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(value[(separator + 1)..], out var port)
            || port is < 1 or > 65535)
        {
            return false;
        }

        endpoint = new IPEndPoint(address, port);
        return true;
    }

    internal sealed class ConfigurationDocument
    {
        public string?[]? EnabledBots { get; set; }
        public string? Endpoint { get; set; }
        public bool ReservationEnabled { get; set; }
        public string? LobbyType { get; set; }
        public int? MaxMembers { get; set; }
        public int? ReservationTimeoutMilliseconds { get; set; }
        public int? HostVersion { get; set; }
    }
}
