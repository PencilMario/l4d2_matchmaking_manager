using static BinaryKeyValues;

internal static class LobbyJoinProtocol
{
    private const string RequestName = "SysSession::RequestJoinData";
    private const string ReplyName = "SysSession::ReplyJoinData";

    public static bool TryGetRequestingSteamId(ReadOnlySpan<byte> request, out ulong steamId)
    {
        steamId = 0;
        return TryParseRequest(request, out var index) &&
            TryGetUInt64(index, $"{RequestName}/id", out steamId) &&
            steamId != 0;
    }

    public static bool TryCreateReply(
        ReadOnlySpan<byte> request,
        string connectString,
        ulong lobbyId,
        ulong ownerSteamId,
        ulong requesterSteamId,
        string gameState,
        out byte[] reply)
    {
        reply = Array.Empty<byte>();
        if (request.Length < 5 || lobbyId == 0 || ownerSteamId == 0 || requesterSteamId == 0)
            return false;
        if (gameState is not ("lobby" or "game"))
            return false;
        if (!TryParseRequest(request, out var index))
            return false;
        if (!TryGetUInt64(index, $"{RequestName}/id", out var requestId) || requestId != requesterSteamId)
            return false;
        if (!TryGetUInt64(index, $"{RequestName}/Settings/Members/machine0/id", out var machineId) || machineId != requesterSteamId)
            return false;
        if (!TryGetUInt64(index, $"{RequestName}/Settings/Members/machine0/player0/xuid", out var playerId) || playerId != requesterSteamId)
            return false;

        var tuVersion = GetString(index, $"{RequestName}/Settings/Members/machine0/tuver", "00000000");
        var playerName = GetString(index, $"{RequestName}/Settings/Members/machine0/player0/name", requesterSteamId.ToString());
        var dlcMask = GetUInt64(index, $"{RequestName}/Settings/Members/machine0/dlcmask", 0);
        var skillVersus = GetInt32(index, $"{RequestName}/Settings/game/sk_versus", 0);

        var machines = new List<BinaryKvEntry>
        {
            BuildMachine("machine0", ownerSteamId, ownerSteamId == requesterSteamId ? playerName : "Lobby Manager", tuVersion, dlcMask),
        };
        if (ownerSteamId != requesterSteamId)
            machines.Add(BuildMachine("machine1", requesterSteamId, playerName, tuVersion, dlcMask));

        var memberChildren = new List<BinaryKvEntry>
        {
            Int32("numMachines", machines.Count),
            Int32("numPlayers", machines.Count),
            Int32("numSlots", 8),
        };
        memberChildren.AddRange(machines);

        var settings = Object("Settings",
            Object("Members", memberChildren.ToArray()),
            Object("game",
                String("mode", "versus"),
                String("map", "c2m1_highway"),
                String("state", gameState),
                Int32("sk_versus", skillVersus)),
            Object("System",
                String("network", "LIVE"),
                String("access", "public"),
                String("netflag", "teamlobby")),
            Object("Options", String("server", "dedicated")),
            Object("Server",
                String("adronline", connectString),
                String("adrlocal", connectString),
                String("connectstring", connectString),
                UInt64("reservationid", lobbyId)));

        var encoded = BinaryKeyValues.Encode(
            Object(ReplyName, UInt64("id", requesterSteamId), settings));
        reply = new byte[4 + encoded.Length];
        request[..4].CopyTo(reply);
        encoded.CopyTo(reply, 4);
        return true;
    }

    public static bool TryCreateRealReply(
        ReadOnlySpan<byte> request,
        string connectString,
        ulong lobbyId,
        ulong ownerSteamId,
        ulong requesterSteamId,
        string? gameMode,
        out byte[] reply)
    {
        reply = Array.Empty<byte>();
        if (request.Length < 5 || lobbyId == 0 || ownerSteamId == 0 || requesterSteamId == 0)
            return false;
        if (!TryParseRequest(request, out var index))
            return false;
        if (!TryGetUInt64(index, $"{RequestName}/id", out var requestId) || requestId != requesterSteamId)
            return false;
        if (!TryGetUInt64(index, $"{RequestName}/Settings/Members/machine0/id", out var machineId) || machineId != requesterSteamId)
            return false;
        if (!TryGetUInt64(index, $"{RequestName}/Settings/Members/machine0/player0/xuid", out var playerId) || playerId != requesterSteamId)
            return false;

        var requester = new SessionMachine(
            requesterSteamId,
            GetString(index, $"{RequestName}/Settings/Members/machine0/player0/name", requesterSteamId.ToString()),
            GetString(index, $"{RequestName}/Settings/Members/machine0/tuver", "00000000"),
            GetUInt64(index, $"{RequestName}/Settings/Members/machine0/dlcmask", 0));
        var machines = new List<SessionMachine>
        {
            new(ownerSteamId, ownerSteamId == requesterSteamId ? requester.PlayerName : "Lobby Manager", requester.TuVersion, requester.DlcMask),
        };
        if (ownerSteamId != requesterSteamId)
            machines.Add(requester);

        BinaryKvEntry settings;
        try
        {
            settings = RealSessionSettings.CreateReplySettings(machines, connectString, lobbyId, gameMode);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var encoded = BinaryKeyValues.Encode(
            Object(ReplyName, UInt64("id", requesterSteamId), settings));
        reply = new byte[4 + encoded.Length];
        request[..4].CopyTo(reply);
        encoded.CopyTo(reply, 4);
        return true;
    }

    private static BinaryKvEntry BuildMachine(
        string machineName,
        ulong steamId,
        string playerName,
        string tuVersion,
        ulong dlcMask) =>
        Object(machineName,
            UInt64("id", steamId),
            Int32("numPlayers", 1),
            UInt64("dlcmask", dlcMask),
            String("tuver", tuVersion),
            Int32("ping", 0),
            Object("player0",
                UInt64("xuid", steamId),
                String("name", playerName)));

    private static bool TryParseRequest(ReadOnlySpan<byte> request, out Dictionary<string, object> index)
    {
        if (request.Length < 5)
        {
            index = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return false;
        }

        return BinaryKeyValues.TryIndex(request, 4, out index, out var bytesConsumed) &&
            bytesConsumed == request.Length - 4 &&
            index.ContainsKey(RequestName);
    }

    private static bool TryGetUInt64(Dictionary<string, object> index, string path, out ulong value)
    {
        value = 0;
        if (!index.TryGetValue(path, out var raw))
            return false;
        if (raw is ulong unsigned)
        {
            value = unsigned;
            return true;
        }
        if (raw is int signed && signed >= 0)
        {
            value = (ulong)signed;
            return true;
        }
        return false;
    }

    private static ulong GetUInt64(Dictionary<string, object> index, string path, ulong defaultValue) =>
        TryGetUInt64(index, path, out var value) ? value : defaultValue;

    private static int GetInt32(Dictionary<string, object> index, string path, int defaultValue) =>
        index.TryGetValue(path, out var value) && value is int integer ? integer : defaultValue;

    private static string GetString(Dictionary<string, object> index, string path, string defaultValue) =>
        index.TryGetValue(path, out var value) && value is string text ? text : defaultValue;

}
