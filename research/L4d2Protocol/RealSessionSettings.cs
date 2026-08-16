using System.Globalization;
using static BinaryKeyValues;

internal static class RealSessionSettings
{
    internal const int NumSlots = 8;
    private static readonly CampaignProfile HistoricalDefault = CampaignProfile.Official[1];

    internal static BinaryKvEntry CreateReservationSettings() => CreateReservationSettings(HistoricalDefault);

    internal static BinaryKvEntry CreateReservationSettings(CampaignProfile profile) =>
        Object("Settings",
            CreateGameSettings(profile),
            Object("Members",
                Int32("numMachines", 1),
                Int32("numPlayers", 1),
                Int32("numSlots", NumSlots)),
            Object("Options",
                String("Server", "official")),
            CreateSystemSettings());

    internal static BinaryKvEntry CreateReplySettings(
        IReadOnlyList<SessionMachine> machines,
        string connectString,
        ulong lobbyId)
    {
        if (machines.Count is < 1 or > NumSlots)
            throw new ArgumentException($"Reply must contain between 1 and {NumSlots} machines.", nameof(machines));
        if (string.IsNullOrWhiteSpace(connectString))
            throw new ArgumentException("Reply connect string must not be empty.", nameof(connectString));
        if (lobbyId == 0)
            throw new ArgumentOutOfRangeException(nameof(lobbyId));

        var memberChildren = new List<BinaryKvEntry>
        {
            Int32("numMachines", machines.Count),
            Int32("numPlayers", machines.Count),
            Int32("numSlots", NumSlots),
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
            CreateGameSettings(HistoricalDefault),
            Object("Members", memberChildren.ToArray()),
            Object("Options", String("Server", "official")),
            CreateSystemSettings(),
            Object("Server",
                String("adronline", connectString),
                String("adrlocal", connectString),
                String("connectstring", connectString),
                UInt64("reservationid", lobbyId)));
    }

    internal static byte[] EncodeReservationSettings() => EncodeReservationSettings(HistoricalDefault);

    internal static byte[] EncodeReservationSettings(CampaignProfile profile) =>
        BinaryKeyValues.EncodeLittleEndian(CreateReservationSettings(profile));

    private static BinaryKvEntry CreateGameSettings(CampaignProfile profile) =>
        Object("Game",
            String("campaign", profile.CampaignId),
            Int32("chapter", 1),
            String("difficulty", "normal"),
            Int32("dlcrequired", 0),
            Int32("maxrounds", 3),
            Object("MissionInfo",
                Int32("addon", 0),
                String("Author", profile.Author),
                Int32("builtin", 1),
                String("DisplayTitle", profile.DisplayTitle),
                Int32("InfectedOnly", 0),
                String("MissionFile", profile.MissionFile),
                Int32("SurvivorSet", profile.SurvivorSet),
                Int32("Version", 1),
                String("Website", "http://store.steampowered.com"),
                Int32("workshopid", 0)),
            String("Mode", "versus"),
            Object("ModeInfo",
                Int32("addon", 0),
                Int32("workshopid", 0)),
            Int32("sk_versus", 35),
            String("state", "game"),
            Int32("vanilla", 1));

    private static BinaryKvEntry CreateSystemSettings() =>
        Object("System",
            String("access", "public"),
            String("lock", string.Empty),
            String("network", "LIVE"));

    internal static Dictionary<string, string> CreateLobbyMetadata() => CreateLobbyMetadata(HistoricalDefault);

    internal static Dictionary<string, string> CreateLobbyMetadata(CampaignProfile profile)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var settings = CreateReservationSettings(profile);
        foreach (var entry in (IReadOnlyList<BinaryKvEntry>)settings.Value)
            AddLobbyMetadata(entry, string.Empty, metadata);
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
