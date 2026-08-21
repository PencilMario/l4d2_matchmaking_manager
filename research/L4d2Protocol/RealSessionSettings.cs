using System.Globalization;
using static BinaryKeyValues;

internal static class RealSessionSettings
{
    internal const int CoopNumSlots = 4;
    internal const int VersusNumSlots = 8;
    private static readonly CampaignProfile DefaultProfile = CampaignProfile.Official[0];

    internal static int GetNumSlots(string? gameMode) =>
        string.Equals(gameMode, "versus", StringComparison.Ordinal) ? VersusNumSlots : CoopNumSlots;

    internal static BinaryKvEntry CreateReservationSettings() => CreateReservationSettings(DefaultProfile);

    internal static BinaryKvEntry CreateReservationSettings(CampaignProfile profile) =>
        Object("Settings",
            CreateGameSettings(profile),
            Object("Members",
                Int32("numMachines", 1),
                Int32("numPlayers", 1),
                Int32("numSlots", CoopNumSlots)),
            Object("Options",
                String("createreason", "searchempty"),
                String("Server", "dedicated")),
            CreateSystemSettings());

    internal static BinaryKvEntry CreateReplySettings(
        IReadOnlyList<SessionMachine> machines,
        string connectString,
        ulong lobbyId,
        string? gameMode = null)
    {
        if (machines.Count is < 1 or > VersusNumSlots)
            throw new ArgumentException($"Reply must contain between 1 and {VersusNumSlots} machines.", nameof(machines));
        if (string.IsNullOrWhiteSpace(connectString))
            throw new ArgumentException("Reply connect string must not be empty.", nameof(connectString));
        if (lobbyId == 0)
            throw new ArgumentOutOfRangeException(nameof(lobbyId));

        var memberChildren = new List<BinaryKvEntry>
        {
            Int32("numMachines", machines.Count),
            Int32("numPlayers", machines.Count),
            Int32("numSlots", GetNumSlots(gameMode)),
        };
        for (var index = 0; index < machines.Count; index++)
        {
            var machine = machines[index];
            if (machine.SteamId == 0)
                throw new ArgumentException("Reply machine Steam IDs must be non-zero.", nameof(machines));

            memberChildren.Add(Object($"machine{index}",
                UInt64("id", machine.SteamId),
                Int32("numPlayers", 1),
                UInt64("dlcmask", machine.DlcMask),
                String("tuver", machine.TuVersion),
                Int32("ping", 0),
                Object("player0",
                    UInt64("xuid", machine.SteamId),
                    String("name", machine.PlayerName))));
        }

        return Object("Settings",
            CreateGameSettings(DefaultProfile, gameMode, "game"),
            Object("Members", memberChildren.ToArray()),
            Object("Options", String("Server", "dedicated")),
            CreateSystemSettings(),
            Object("Server",
                String("adronline", connectString),
                String("adrlocal", connectString),
                String("connectstring", connectString),
                UInt64("reservationid", lobbyId)));
    }

    internal static byte[] EncodeReservationSettings() => EncodeReservationSettings(DefaultProfile);

    internal static byte[] EncodeReservationSettings(CampaignProfile profile) =>
        BinaryKeyValues.EncodeLittleEndian(CreateReservationSettings(profile));

    private static BinaryKvEntry CreateGameSettings(
        CampaignProfile profile,
        string? gameMode = null,
        string gameState = "lobby") =>
        Object("Game",
            String("campaign", profile.CampaignId),
            Int32("chapter", 1),
            String("difficulty", "Impossible"),
            Int32("dlcrequired", 0),
            Int32("maxrounds", 3),
            Object("MissionInfo",
                String("Author", profile.Author),
                Int32("BuiltIn", 1),
                String("DisplayTitle", profile.DisplayTitle),
                Int32("Version", 1),
                String("Website", "http://store.steampowered.com/app/500/")),
            String("mode", string.Equals(gameMode, "versus", StringComparison.Ordinal) ? "versus" : "coop"),
            String("state", gameState));

    private static BinaryKvEntry CreateSystemSettings() =>
        Object("System",
            String("access", "public"),
            String("lock", string.Empty),
            String("network", "LIVE"));

    internal static Dictionary<string, string> CreateLobbyMetadata() => CreateLobbyMetadata(DefaultProfile);

    internal static Dictionary<string, string> CreateLobbyMetadata(
        CampaignProfile profile,
        string? gameMode = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var settings = CreateReservationSettings(profile);
        foreach (var entry in (IReadOnlyList<BinaryKvEntry>)settings.Value)
            AddLobbyMetadata(entry, string.Empty, metadata);

        var normalizedMode = string.Equals(gameMode, "versus", StringComparison.Ordinal) ? "versus" : "coop";
        metadata["Members:numSlots"] = GetNumSlots(normalizedMode).ToString(CultureInfo.InvariantCulture);
        metadata["Game:mode"] = normalizedMode;

        return metadata;
    }

    private static void AddLobbyMetadata(
        BinaryKvEntry entry,
        string parentPath,
        Dictionary<string, string> metadata)
    {
        var path = string.IsNullOrEmpty(parentPath)
            ? entry.Name
            : $"{parentPath}:{entry.Name}";

        switch (entry.Type)
        {
            case TypeObject:
                foreach (var child in (IReadOnlyList<BinaryKvEntry>)entry.Value)
                    AddLobbyMetadata(child, path, metadata);
                break;
            case TypeString:
                metadata.Add(path, (string)entry.Value);
                break;
            case TypeInt32:
                metadata.Add(path, ((int)entry.Value).ToString(CultureInfo.InvariantCulture));
                break;
            case TypeUInt64:
                metadata.Add(path, ((ulong)entry.Value).ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported lobby metadata KeyValues type {entry.Type}.");
        }
    }
}

internal readonly record struct SessionMachine(
    ulong SteamId,
    string PlayerName,
    string TuVersion,
    ulong DlcMask);
