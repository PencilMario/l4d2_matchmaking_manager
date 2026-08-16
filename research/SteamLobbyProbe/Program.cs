using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    private const int LobbyTypePrivate = 0;
    private const int LobbyTypeFriendsOnly = 1;
    private const int LobbyTypePublic = 2;
    private const int LobbyTypeInvisible = 3;
    private const int LobbyEnterCallback = 504;
    private const int LobbyMatchListCallback = 510;
    private const int LobbyCreatedCallback = 513;
    private const int LobbyCreatedCompactPayloadSize = 12;
    private const int LobbyCreatedPaddedPayloadSize = 16;
    private const int SteamApiCallCompletedCallback = 703;

    private static int Main(string[] args)
    {
        if (TryRunHealthCheck(args, out var healthCheckExitCode))
            return healthCheckExitCode;

        if (ReservationCommand.TryRun(args, out var reservationExitCode))
            return reservationExitCode;

        var dllPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : throw new ArgumentException("Pass the full path to L4D2's bin\\steam_api.dll.");
        var protocolReplyMode = args.Length > 1 && string.Equals(args[1], "protocol-reply", StringComparison.OrdinalIgnoreCase);
        var realProtocolReplyMode = args.Length > 1 && string.Equals(args[1], "protocol-reply-real", StringComparison.OrdinalIgnoreCase);
        if (protocolReplyMode || realProtocolReplyMode)
            return RunProtocolReply(args, realProtocolReplyMode);

        var transferOwnerMode = args.Length > 1 && string.Equals(args[1], "transfer-owner", StringComparison.OrdinalIgnoreCase);
        if (transferOwnerMode && args.Length < 4)
            throw new ArgumentException("Transfer-owner mode requires lobbyId and newOwnerSteamId.");
        var transferLobbyId = transferOwnerMode
            ? ulong.Parse(args[2], CultureInfo.InvariantCulture)
            : 0;
        var transferNewOwnerSteamId = transferOwnerMode
            ? ulong.Parse(args[3], CultureInfo.InvariantCulture)
            : 0;

        var leaveLobbyMode = args.Length > 1 && string.Equals(args[1], "leave-lobby", StringComparison.OrdinalIgnoreCase);
        if (leaveLobbyMode && args.Length < 3)
            throw new ArgumentException("Leave-lobby mode requires lobbyId.");
        var leaveLobbyId = leaveLobbyMode
            ? ulong.Parse(args[2], CultureInfo.InvariantCulture)
            : 0;
        var leaveSettleSeconds = leaveLobbyMode && args.Length > 3
            ? int.Parse(args[3], CultureInfo.InvariantCulture)
            : 2;
        if (leaveSettleSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(leaveSettleSeconds), "Settle seconds must be non-negative.");

        var createLobbyHoldMode = args.Length > 1 && string.Equals(args[1], "create-lobby-hold", StringComparison.OrdinalIgnoreCase);

        var joinLobbyHoldMode = args.Length > 1 && string.Equals(args[1], "join-lobby-hold", StringComparison.OrdinalIgnoreCase);
        if (joinLobbyHoldMode && args.Length < 3)
            throw new ArgumentException("Join-lobby-hold mode requires lobbyId.");
        var joinLobbyId = joinLobbyHoldMode
            ? ulong.Parse(args[2], CultureInfo.InvariantCulture)
            : 0;
        var joinKeepaliveSeconds = joinLobbyHoldMode && args.Length > 3
            ? int.Parse(args[3], CultureInfo.InvariantCulture)
            : -1;

        var serverMode = args.Length > 1 && string.Equals(args[1], "server", StringComparison.OrdinalIgnoreCase);
        var reservedServerMode = args.Length > 1 && string.Equals(args[1], "server-reserved", StringComparison.OrdinalIgnoreCase);
        if (serverMode && args.Length < 3)
            throw new ArgumentException("Server mode requires an IPv4 endpoint in the form ip:port.");
        if (reservedServerMode && args.Length < 3)
            throw new ArgumentException("server-reserved mode requires an IPv4 endpoint in the form ip:port");

        var endpoint = serverMode || reservedServerMode ? ParseServerEndpoint(args[2]) : default;
        var lobbyType = serverMode || reservedServerMode
            ? (args.Length > 3 ? ParseLobbyType(args[3]) : LobbyTypePublic)
            : createLobbyHoldMode
                ? (args.Length > 2 ? ParseLobbyType(args[2]) : LobbyTypePrivate)
            : transferOwnerMode
                ? LobbyTypePrivate
            : leaveLobbyMode
                ? LobbyTypePrivate
            : joinLobbyHoldMode
                ? LobbyTypePrivate
            : (args.Length > 1 ? ParseLobbyType(args[1]) : LobbyTypePrivate);
        var keepaliveSeconds = serverMode || reservedServerMode
            ? (args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : -1)
            : createLobbyHoldMode
                ? (args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : -1)
                : -1;
        var serverGameState = serverMode && args.Length > 5
            ? ParseGameState(args[5])
            : reservedServerMode
                ? "game"
                : "lobby";
        var useManualDispatch = serverMode || reservedServerMode || transferOwnerMode || leaveLobbyMode || createLobbyHoldMode || joinLobbyHoldMode ||
            args.Length <= 2 ||
            !string.Equals(args[2], "direct", StringComparison.OrdinalIgnoreCase);
        var appRoot = AppContext.BaseDirectory;
        var oldCurrentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(appRoot);

        nint module = 0;
        SteamApi? api = null;
        nint matchmaking = 0;
        var pipe = 0;
        ulong activeLobbyId = 0;
        try
        {
            module = NativeLibrary.Load(dllPath);
            api = SteamApi.Load(module);
            if (!api.Init())
            {
                Console.WriteLine("SteamAPI_Init=false");
                return 2;
            }

            if (useManualDispatch)
            {
                api.ManualDispatchInit();
                Console.WriteLine("Callback dispatch=manual");
            }

            var appId = api.GetUtilsAppId();
            Console.WriteLine($"SteamAPI_Init=true AppID={appId}");
            var user = api.SteamUser();
            var ownerSteamId = api.GetSteamId(user);
            Console.WriteLine($"SteamUser BLoggedOn={api.IsLoggedOn(user)} steam_id={ownerSteamId}");
            matchmaking = api.SteamMatchmaking();
            var utils = api.SteamUtils();
            pipe = useManualDispatch ? api.GetHSteamPipe() : 0;

            if (transferOwnerMode)
            {
                return TransferLobbyOwner(
                    api,
                    matchmaking,
                    pipe,
                    appId,
                    ownerSteamId,
                    transferLobbyId,
                    transferNewOwnerSteamId);
            }

            if (leaveLobbyMode)
            {
                return LeaveLobbyAndObserve(
                    api,
                    matchmaking,
                    pipe,
                    ownerSteamId,
                    leaveLobbyId,
                    leaveSettleSeconds);
            }

            if (joinLobbyHoldMode)
            {
                if (joinLobbyId == 0)
                    throw new ArgumentException("Lobby ID must be non-zero.");

                var joinCall = api.JoinLobby(matchmaking, joinLobbyId);
                Console.WriteLine($"JoinLobby call={joinCall} lobby_id={joinLobbyId}");
                var joinResult = WaitForApiCallManual(
                    api,
                    pipe,
                    utils,
                    joinCall,
                    LobbyEnterCallback,
                    24);
                var enteredLobbyId = joinResult.Raw.Length >= 8
                    ? BitConverter.ToUInt64(joinResult.Raw, 0)
                    : 0;
                var enterResponse = joinResult.Raw.Length >= 20
                    ? BitConverter.ToUInt32(joinResult.Raw, 16)
                    : 0;
                Console.WriteLine(
                    $"JoinLobbyHold lobby_id={enteredLobbyId} response={enterResponse} seconds={joinKeepaliveSeconds}");
                if (!joinResult.Ok || joinResult.Failed || enteredLobbyId != joinLobbyId || enterResponse != 1)
                    return 10;

                activeLobbyId = joinLobbyId;
                if (!LogLobbyMembership(api, matchmaking, joinLobbyId, ownerSteamId, "joined"))
                    return 11;
                KeepLobbyAlive(api, matchmaking, pipe, useManualDispatch, joinKeepaliveSeconds, null);
                return 0;
            }

            var listCall = api.RequestLobbyList(matchmaking);
            Console.WriteLine($"RequestLobbyList call={listCall}");
            var listResult = useManualDispatch
                ? WaitForApiCallManual(api, pipe, utils, listCall, LobbyMatchListCallback, 4)
                : WaitForApiCall(api, utils, listCall, LobbyMatchListCallback, 4);
            var lobbyCount = listResult.Raw.Length >= 4 ? BitConverter.ToUInt32(listResult.Raw, 0) : 0;
            Console.WriteLine($"LobbyMatchList ok={listResult.Ok} failed={listResult.Failed} count={lobbyCount}");

            var call = api.CreateLobby(matchmaking, lobbyType, 8);
            Console.WriteLine($"CreateLobby call={call} type={lobbyType} max_members=8");

            var result = useManualDispatch
                ? WaitForLobbyCreatedManual(api, pipe, utils, call)
                : WaitForLobbyCreated(api, utils, call);
            if (result.Result != 1 || result.LobbyId == 0)
                return 3;
            activeLobbyId = result.LobbyId;

            if (createLobbyHoldMode)
            {
                Console.WriteLine(
                    $"CreateLobbyHold lobby_id={result.LobbyId} type={lobbyType} seconds={keepaliveSeconds}");
            }

            var campaignProfile = reservedServerMode ? CampaignProfile.SelectRandom() : null;
            var values = reservedServerMode
                ? RealSessionSettings.CreateLobbyMetadata(campaignProfile!)
                : serverMode
                    ? BuildServerLobbyData(endpoint, result.LobbyId, serverGameState)
                    : BuildProbeLobbyData();

            foreach (var pair in values)
            {
                var ok = api.SetLobbyData(matchmaking, result.LobbyId, pair.Key, pair.Value);
                Console.WriteLine($"SetLobbyData key={pair.Key} value={pair.Value} ok={ok}");
            }

            Thread.Sleep(500);
            if (!useManualDispatch)
                api.RunCallbacks();
            foreach (var pair in values)
            {
                var value = api.GetLobbyData(matchmaking, result.LobbyId, pair.Key);
                Console.WriteLine($"GetLobbyData key={pair.Key} value={value}");
            }

            if (serverMode)
            {
                api.SetLobbyGameServer(matchmaking, result.LobbyId, endpoint.HostOrderIp, endpoint.Port, 0);
                Console.WriteLine($"SetLobbyGameServer lobby_id={result.LobbyId} ip={endpoint.ConnectString} steam_server_id=0");
                if (!WaitForLobbyGameServer(api, matchmaking, result.LobbyId, endpoint, pipe, useManualDispatch))
                    return 4;

                Console.WriteLine($"JoinURI=steam://joinlobby/{api.GetUtilsAppId()}/{result.LobbyId}/{ownerSteamId}");
                Console.WriteLine($"ConnectLobbyArg=+connect_lobby {result.LobbyId}");
                KeepLobbyAlive(
                    api,
                    matchmaking,
                    pipe,
                    useManualDispatch,
                    keepaliveSeconds,
                    new ServerLobbyContext(result.LobbyId, ownerSteamId, endpoint, serverGameState, LobbySettingsProfile.Legacy));
            }
            else if (reservedServerMode)
            {
                var settings = RealSessionSettings.EncodeReservationSettings(campaignProfile!);
                Console.WriteLine($"ReservationSettings lobby_id={result.LobbyId} size={settings.Length}");
                var reservedReservationExitCode = ReservationCommand.RunLive(
                    endpoint.ToIPEndPoint(),
                    result.LobbyId,
                    settings,
                    5000,
                    ReservationProtocol.DefaultHostVersion);
                Console.WriteLine($"ReservationComplete lobby_id={result.LobbyId} exit_code={reservedReservationExitCode}");
                if (reservedReservationExitCode is not (0 or 8))
                    return reservedReservationExitCode;

                Console.WriteLine($"LobbyId={result.LobbyId}");
                Console.WriteLine($"SettingsSize={settings.Length}");
                Console.WriteLine($"JoinURI=steam://joinlobby/{api.GetUtilsAppId()}/{result.LobbyId}/{ownerSteamId}");
                Console.WriteLine($"ConnectLobbyArg=+connect_lobby {result.LobbyId}");
                Console.WriteLine("verification=server_status_required");
                KeepLobbyAlive(
                    api,
                    matchmaking,
                    pipe,
                    useManualDispatch,
                    keepaliveSeconds,
                    new ServerLobbyContext(result.LobbyId, ownerSteamId, endpoint, serverGameState, LobbySettingsProfile.Real));
            }
            else if (createLobbyHoldMode)
            {
                KeepLobbyAlive(api, matchmaking, pipe, useManualDispatch, keepaliveSeconds, null);
            }

            return 0;
        }
        finally
        {
            if (api is not null && matchmaking != 0 && activeLobbyId != 0)
            {
                try
                {
                    api.LeaveLobby(matchmaking, activeLobbyId);
                    if (useManualDispatch)
                        PumpCallbacks(api, matchmaking, pipe, true);
                    else
                        api.RunCallbacks();
                    Console.WriteLine("LeaveLobby complete");
                }
                catch
                {
                    // Keep the probe's original result if lobby cleanup is unavailable.
                }
            }

            if (module != 0)
            {
                try
                {
                    (api ?? SteamApi.Load(module)).Shutdown();
                }
                catch
                {
                    // Keep the probe's original result if shutdown is unavailable.
                }

                NativeLibrary.Free(module);
            }

            Directory.SetCurrentDirectory(oldCurrentDirectory);
        }
    }

    private static bool TryRunHealthCheck(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length < 2 || !string.Equals(args[1], "health-check", StringComparison.OrdinalIgnoreCase))
            return false;

        var libraryPath = Path.GetFullPath(args[0]);
        var oldCurrentDirectory = Directory.GetCurrentDirectory();
        nint module = 0;
        SteamApi? api = null;
        var initialized = false;

        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            module = NativeLibrary.Load(libraryPath);
            api = SteamApi.Load(module);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            return WriteHealthCheckResult(false, "steam_api_load_failed", out exitCode);
        }

        try
        {
            initialized = api.Init();
            if (!initialized)
                return WriteHealthCheckResult(false, "steam_api_init_failed", out exitCode);

            api.ManualDispatchInit();
            var appId = api.GetUtilsAppId();
            if (appId != 550)
                return WriteHealthCheckResult(false, "appid_mismatch", out exitCode, appId);

            var user = api.SteamUser();
            var loggedOn = api.IsLoggedOn(user);
            var steamId = api.GetSteamId(user);
            if (!loggedOn || steamId == 0)
                return WriteHealthCheckResult(false, "steam_not_logged_on", out exitCode, appId);

            var matchmaking = api.SteamMatchmaking();
            var utils = api.SteamUtils();
            var pipe = api.GetHSteamPipe();
            var listCall = api.RequestLobbyList(matchmaking);
            if (listCall == 0)
                return WriteHealthCheckResult(false, "lobby_list_request_failed", out exitCode, appId);

            var listResult = WaitForApiCallManual(api, pipe, utils, listCall, LobbyMatchListCallback, 4);
            if (!listResult.Ok)
            {
                var failure = listResult.Failed ? "lobby_list_callback_failed" : "lobby_list_timeout";
                return WriteHealthCheckResult(false, failure, out exitCode, appId);
            }

            var lobbyCount = listResult.Raw.Length >= 4 ? BitConverter.ToUInt32(listResult.Raw, 0) : 0;
            return WriteHealthCheckResult(true, null, out exitCode, appId, lobbyCount);
        }
        catch (Exception)
        {
            return WriteHealthCheckResult(false, "steam_api_runtime_failed", out exitCode);
        }
        finally
        {
            if (initialized && api is not null)
            {
                try
                {
                    api.Shutdown();
                }
                catch
                {
                    // The health result is already emitted and must remain machine-readable.
                }
            }

            if (module != 0)
                NativeLibrary.Free(module);

            Directory.SetCurrentDirectory(oldCurrentDirectory);
        }
    }

    private static bool WriteHealthCheckResult(
        bool ready,
        string? failure,
        out int exitCode,
        uint? appId = null,
        uint? lobbyCount = null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ready,
            failure,
            checks = new
            {
                steamApiInit = ready || failure is not ("steam_api_load_failed" or "steam_api_init_failed") ? "ok" : "failed",
                appId,
                loggedOn = ready || failure is "lobby_list_request_failed" or "lobby_list_callback_failed" or "lobby_list_timeout"
                    ? "ok"
                    : "unknown",
                manualDispatch = ready || failure is "lobby_list_request_failed" or "lobby_list_callback_failed" or "lobby_list_timeout"
                    ? "ok"
                    : "unknown",
                lobbyListCallback = ready ? "ok" : "failed",
                lobbyCount
            }
        }));
        exitCode = ready ? 0 : 2;
        return true;
    }

    private static Dictionary<string, string> BuildProbeLobbyData() => new()
    {
        ["game:mode"] = "coop",
        ["game:map"] = "c1m1_hotel",
        ["game:state"] = "lobby",
        ["system:network"] = "LIVE",
        ["system:access"] = "public",
        ["options:server"] = "listen",
        ["members:numSlots"] = "8",
        ["members:numPlayers"] = "1",
        ["members:numMachines"] = "1",
    };

    private static Dictionary<string, string> BuildServerLobbyData(
        ServerEndpoint endpoint,
        ulong lobbyId,
        string gameState) => new()
    {
        ["game:mode"] = "versus",
        ["game:map"] = "c2m1_highway",
        ["game:state"] = gameState,
        ["system:network"] = "LIVE",
        ["system:access"] = "public",
        ["options:server"] = "dedicated",
        ["server:adronline"] = endpoint.ConnectString,
        ["server:adrlocal"] = endpoint.ConnectString,
        ["server:connectstring"] = endpoint.ConnectString,
        ["server:reservationid"] = lobbyId.ToString(CultureInfo.InvariantCulture),
        ["members:numSlots"] = "8",
        ["members:numPlayers"] = "1",
        ["members:numMachines"] = "1",
    };

    private static int RunProtocolReply(string[] args, bool realProfile)
    {
        if (args.Length < 5)
            throw new ArgumentException("Protocol reply mode requires requestHex, ip:port, and lobbyId.");

        var request = Convert.FromHexString(args[2]);
        var endpoint = ParseServerEndpoint(args[3]);
        var lobbyId = ulong.Parse(args[4], CultureInfo.InvariantCulture);
        if (!LobbyJoinProtocol.TryGetRequestingSteamId(request, out var requesterSteamId))
            throw new ArgumentException("The payload is not a supported SysSession::RequestJoinData message.");
        var ownerSteamId = args.Length > 5
            ? ulong.Parse(args[5], CultureInfo.InvariantCulture)
            : requesterSteamId;
        var gameState = args.Length > 6
            ? ParseGameState(args[6])
            : "lobby";
        var replyCreated = realProfile
            ? LobbyJoinProtocol.TryCreateRealReply(
                request,
                endpoint.ConnectString,
                lobbyId,
                ownerSteamId,
                requesterSteamId,
                out var reply)
            : LobbyJoinProtocol.TryCreateReply(
                request,
                endpoint.ConnectString,
                lobbyId,
                ownerSteamId,
                requesterSteamId,
                gameState,
                out reply);
        if (!replyCreated)
            throw new InvalidOperationException("Failed to encode SysSession::ReplyJoinData.");

        Console.WriteLine(
            $"ReplyJoinData recipient={requesterSteamId} size={reply.Length} raw={Convert.ToHexString(reply)}");
        return 0;
    }

    private static int TransferLobbyOwner(
        SteamApi api,
        nint matchmaking,
        int pipe,
        uint appId,
        ulong currentUserSteamId,
        ulong lobbyId,
        ulong newOwnerSteamId)
    {
        if (lobbyId == 0 || newOwnerSteamId == 0)
            throw new ArgumentException("Lobby and new owner Steam IDs must be non-zero.");

        var requested = api.RequestLobbyData(matchmaking, lobbyId);
        Console.WriteLine($"RequestLobbyData phase=before lobby_id={lobbyId} ok={requested}");
        var beforeOwner = WaitForLobbyOwner(api, matchmaking, pipe, lobbyId, 0);
        var beforeConnectString = api.GetLobbyData(matchmaking, lobbyId, "server:connectstring");
        var beforeReservationId = api.GetLobbyData(matchmaking, lobbyId, "server:reservationid");
        Console.WriteLine(
            $"LobbyOwnership before lobby_id={lobbyId} owner={beforeOwner} current_user={currentUserSteamId}");
        Console.WriteLine($"LobbyData before key=server:connectstring value={beforeConnectString}");
        Console.WriteLine($"LobbyData before key=server:reservationid value={beforeReservationId}");
        if (!requested || beforeOwner == 0 || beforeOwner != currentUserSteamId)
            return 5;

        var transferred = api.SetLobbyOwner(matchmaking, lobbyId, newOwnerSteamId);
        Console.WriteLine($"SetLobbyOwner lobby_id={lobbyId} new_owner={newOwnerSteamId} ok={transferred}");
        if (!transferred)
            return 6;

        var afterOwner = WaitForLobbyOwner(api, matchmaking, pipe, lobbyId, newOwnerSteamId);
        Console.WriteLine($"LobbyOwnership after lobby_id={lobbyId} owner={afterOwner}");
        if (afterOwner != newOwnerSteamId)
            return 7;

        requested = api.RequestLobbyData(matchmaking, lobbyId);
        Console.WriteLine($"RequestLobbyData phase=after lobby_id={lobbyId} ok={requested}");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            PumpCallbacks(api, matchmaking, pipe, true);
            Thread.Sleep(50);
        }

        var afterConnectString = api.GetLobbyData(matchmaking, lobbyId, "server:connectstring");
        var afterReservationId = api.GetLobbyData(matchmaking, lobbyId, "server:reservationid");
        var connectStringPreserved = string.Equals(beforeConnectString, afterConnectString, StringComparison.Ordinal);
        var reservationIdPreserved = string.Equals(beforeReservationId, afterReservationId, StringComparison.Ordinal);
        Console.WriteLine($"LobbyData after key=server:connectstring value={afterConnectString}");
        Console.WriteLine($"LobbyData after key=server:reservationid value={afterReservationId}");
        Console.WriteLine(
            $"MetadataPreserved connectstring={connectStringPreserved} reservationid={reservationIdPreserved}");
        Console.WriteLine($"NewOwnerJoinURI=steam://joinlobby/{appId}/{lobbyId}/{newOwnerSteamId}");
        return requested && connectStringPreserved && reservationIdPreserved ? 0 : 8;
    }

    private static int LeaveLobbyAndObserve(
        SteamApi api,
        nint matchmaking,
        int pipe,
        ulong currentUserSteamId,
        ulong lobbyId,
        int settleSeconds)
    {
        if (lobbyId == 0)
            throw new ArgumentException("Lobby ID must be non-zero.");

        var requested = api.RequestLobbyData(matchmaking, lobbyId);
        Console.WriteLine($"RequestLobbyData phase=before lobby_id={lobbyId} ok={requested}");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            PumpCallbacks(api, matchmaking, pipe, true);
            Thread.Sleep(50);
        }

        var before = LogLobbyMembership(api, matchmaking, lobbyId, currentUserSteamId, "before");
        api.LeaveLobby(matchmaking, lobbyId);
        Console.WriteLine($"LeaveLobby requested lobby_id={lobbyId} settle_seconds={settleSeconds}");

        var deadline = Environment.TickCount64 + checked(settleSeconds * 1000L);
        do
        {
            PumpCallbacks(api, matchmaking, pipe, true);
            Thread.Sleep(50);
        }
        while (Environment.TickCount64 < deadline);

        var after = LogLobbyMembership(api, matchmaking, lobbyId, currentUserSteamId, "after");
        return before && !after ? 0 : 9;
    }

    private static bool LogLobbyMembership(
        SteamApi api,
        nint matchmaking,
        ulong lobbyId,
        ulong currentUserSteamId,
        string phase)
    {
        var ownerSteamId = api.GetLobbyOwner(matchmaking, lobbyId);
        var memberCount = api.GetNumLobbyMembers(matchmaking, lobbyId);
        var members = new List<ulong>(Math.Max(memberCount, 0));
        var isMember = false;
        for (var index = 0; index < memberCount; index++)
        {
            var memberSteamId = api.GetLobbyMemberByIndex(matchmaking, lobbyId, index);
            if (memberSteamId == 0)
                continue;
            members.Add(memberSteamId);
            isMember |= memberSteamId == currentUserSteamId;
        }

        var memberList = members.Count == 0
            ? "<none>"
            : string.Join(',', members.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(
            $"LobbyMembership {phase} lobby_id={lobbyId} current_user={currentUserSteamId} owner={ownerSteamId} " +
            $"member_count={memberCount} valid_member_count={members.Count} is_member={isMember} members={memberList}");
        return isMember;
    }

    private static ulong WaitForLobbyOwner(
        SteamApi api,
        nint matchmaking,
        int pipe,
        ulong lobbyId,
        ulong expectedOwnerSteamId)
    {
        ulong ownerSteamId = 0;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            PumpCallbacks(api, matchmaking, pipe, true);
            ownerSteamId = api.GetLobbyOwner(matchmaking, lobbyId);
            if (ownerSteamId != 0 && (expectedOwnerSteamId == 0 || ownerSteamId == expectedOwnerSteamId))
                break;
            Thread.Sleep(50);
        }

        return ownerSteamId;
    }

    private static bool WaitForLobbyGameServer(
        SteamApi api,
        nint matchmaking,
        ulong lobbyId,
        ServerEndpoint expected,
        int pipe,
        bool useManualDispatch)
    {
        uint ip = 0;
        ushort port = 0;
        ulong serverId = 0;
        var bound = false;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            PumpCallbacks(api, matchmaking, pipe, useManualDispatch);
            bound = api.GetLobbyGameServer(matchmaking, lobbyId, out ip, out port, out serverId);
            if (bound && ip == expected.HostOrderIp && port == expected.Port)
                break;
            Thread.Sleep(50);
        }

        var endpoint = ip == 0 || port == 0 ? $"{ip}:{port}" : FormatServerEndpoint(ip, port);
        Console.WriteLine($"GetLobbyGameServer ok={bound} lobby_id={lobbyId} ip={endpoint} steam_server_id={serverId}");
        return bound && ip == expected.HostOrderIp && port == expected.Port;
    }

    private static void KeepLobbyAlive(
        SteamApi api,
        nint matchmaking,
        int pipe,
        bool useManualDispatch,
        int seconds,
        ServerLobbyContext? serverContext)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            Console.WriteLine($"Keepalive begin seconds={seconds}");
            var deadline = seconds < 0
                ? long.MaxValue
                : Environment.TickCount64 + checked(seconds * 1000L);
            while (!cancellation.IsCancellationRequested && Environment.TickCount64 < deadline)
            {
                PumpCallbacks(api, matchmaking, pipe, useManualDispatch, serverContext);
                Thread.Sleep(50);
            }
            PumpCallbacks(api, matchmaking, pipe, useManualDispatch, serverContext);
            Console.WriteLine($"Keepalive complete seconds={seconds}");
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static void PumpCallbacks(
        SteamApi api,
        nint matchmaking,
        int pipe,
        bool useManualDispatch,
        ServerLobbyContext? serverContext = null)
    {
        if (!useManualDispatch)
        {
            api.RunCallbacks();
            return;
        }

        api.ManualDispatchRunFrame(pipe);
        var callback = new CallbackMsg();
        while (api.ManualDispatchGetNextCallback(pipe, ref callback))
        {
            try
            {
                var includeRaw = callback.Callback is 506 or 507 or 509;
                var rawSize = includeRaw ? Math.Clamp(callback.ParamSize, 0, 4096) : 0;
                var raw = new byte[rawSize];
                if (callback.Param != 0 && raw.Length > 0)
                    Marshal.Copy(callback.Param, raw, 0, raw.Length);

                var rawSuffix = includeRaw ? $" raw={Convert.ToHexString(raw)}" : string.Empty;
                Console.WriteLine($"DispatchCallback id={callback.Callback} size={callback.ParamSize}{rawSuffix}");
                if (callback.Callback == 506)
                    LogLobbyChatUpdate(raw);
                else if (callback.Callback == 507)
                    LogLobbyChatEntry(api, matchmaking, raw, serverContext);
            }
            finally
            {
                api.ManualDispatchFreeLastCallback(pipe);
            }
        }
    }

    private static void LogLobbyChatUpdate(byte[] raw)
    {
        if (raw.Length < 28)
            return;

        var lobbyId = BitConverter.ToUInt64(raw, 0);
        var changedUserId = BitConverter.ToUInt64(raw, 8);
        var actorUserId = BitConverter.ToUInt64(raw, 16);
        var state = BitConverter.ToUInt32(raw, 24);
        Console.WriteLine(
            $"LobbyChatUpdate lobby_id={lobbyId} changed_user={changedUserId} actor_user={actorUserId} state=0x{state:X8}");
    }

    private static void LogLobbyChatEntry(
        SteamApi api,
        nint matchmaking,
        byte[] callbackRaw,
        ServerLobbyContext? serverContext)
    {
        if (callbackRaw.Length < 24)
            return;

        var lobbyId = BitConverter.ToUInt64(callbackRaw, 0);
        var callbackUserId = BitConverter.ToUInt64(callbackRaw, 8);
        var callbackEntryType = callbackRaw[16];
        var chatId = BitConverter.ToInt32(callbackRaw, 20);
        var buffer = Marshal.AllocHGlobal(4096);
        try
        {
            var size = api.GetLobbyChatEntry(
                matchmaking,
                lobbyId,
                chatId,
                out var senderUserId,
                buffer,
                4096,
                out var entryType);
            var message = new byte[Math.Clamp(size, 0, 4096)];
            if (message.Length > 0)
                Marshal.Copy(buffer, message, 0, message.Length);
            Console.WriteLine(
                $"LobbyChatEntry lobby_id={lobbyId} callback_user={callbackUserId} sender_user={senderUserId} " +
                $"callback_type={callbackEntryType} entry_type={entryType} chat_id={chatId} size={size} raw={Convert.ToHexString(message)}");

            if (serverContext is not { } context ||
                lobbyId != context.LobbyId ||
                entryType != 1 ||
                !TryCreateReply(context, message, senderUserId, out var reply))
                return;

            var sent = api.SendLobbyChatMsg(matchmaking, lobbyId, reply);
            Console.WriteLine(
                $"ReplyJoinData recipient={senderUserId} size={reply.Length} sent={sent} raw={Convert.ToHexString(reply)}");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryCreateReply(
        ServerLobbyContext context,
        ReadOnlySpan<byte> request,
        ulong requesterSteamId,
        out byte[] reply)
    {
        if (context.SettingsProfile == LobbySettingsProfile.Real)
        {
            return LobbyJoinProtocol.TryCreateRealReply(
                request,
                context.Endpoint.ConnectString,
                context.LobbyId,
                context.OwnerSteamId,
                requesterSteamId,
                out reply);
        }

        return LobbyJoinProtocol.TryCreateReply(
            request,
            context.Endpoint.ConnectString,
            context.LobbyId,
            context.OwnerSteamId,
            requesterSteamId,
            context.GameState,
            out reply);
    }

    private static ServerEndpoint ParseServerEndpoint(string value)
    {
        var separator = value.LastIndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            throw new ArgumentException("Server endpoint must use the form IPv4:port.", nameof(value));

        if (!IPAddress.TryParse(value[..separator], out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Server endpoint must contain an IPv4 address.", nameof(value));

        if (!ushort.TryParse(value[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port == 0)
            throw new ArgumentException("Server endpoint must contain a port from 1 to 65535.", nameof(value));

        var bytes = address.GetAddressBytes();
        var hostOrderIp =
            ((uint)bytes[0] << 24) |
            ((uint)bytes[1] << 16) |
            ((uint)bytes[2] << 8) |
            bytes[3];
        return new ServerEndpoint($"{address}:{port}", address, hostOrderIp, port);
    }

    private static string FormatServerEndpoint(uint hostOrderIp, ushort port) =>
        $"{(hostOrderIp >> 24) & 0xFF}.{(hostOrderIp >> 16) & 0xFF}.{(hostOrderIp >> 8) & 0xFF}.{hostOrderIp & 0xFF}:{port}";

    private static LobbyCreatedResult WaitForLobbyCreated(SteamApi api, nint utils, ulong call)
    {
        return DecodeLobbyCreated(WaitForApiCall(api, utils, call, LobbyCreatedCallback, LobbyCreatedPaddedPayloadSize));
    }

    private static LobbyCreatedResult WaitForLobbyCreatedManual(SteamApi api, int pipe, nint utils, ulong call)
    {
        return DecodeLobbyCreated(WaitForApiCallManual(api, pipe, utils, call, LobbyCreatedCallback, LobbyCreatedCompactPayloadSize));
    }

    private static ApiCallResult WaitForApiCall(SteamApi api, nint utils, ulong call, int callbackId, int callbackSize)
    {
        var failed = (byte)0;
        for (var i = 0; i < 100; i++)
        {
            if (api.IsApiCallCompleted(utils, call, ref failed))
            {
                var failureReason = api.GetApiCallFailureReason(utils, call);
                var buffer = Marshal.AllocHGlobal(callbackSize);
                try
                {
                    var resultOk = api.GetApiCallResult(utils, call, buffer, callbackSize, callbackId, ref failed);
                    var raw = new byte[callbackSize];
                    Marshal.Copy(buffer, raw, 0, raw.Length);
                    Console.WriteLine($"APICallResult callback={callbackId} ok={resultOk} failed={failed} failure_reason={failureReason} raw={Convert.ToHexString(raw)}");
                    return new ApiCallResult(resultOk != 0, failed != 0, raw);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            Thread.Sleep(100);
        }

        return new ApiCallResult(false, failed != 0, Array.Empty<byte>());
    }

    private static ApiCallResult WaitForApiCallManual(SteamApi api, int pipe, nint utils, ulong call, int callbackId, int callbackSize)
    {
        ApiCallResult? failedCallResult = null;
        for (var i = 0; i < 100; i++)
        {
            api.ManualDispatchRunFrame(pipe);
            var callback = new CallbackMsg();
            while (api.ManualDispatchGetNextCallback(pipe, ref callback))
            {
                try
                {
                    if (callback.Callback != SteamApiCallCompletedCallback && callback.Callback != callbackId)
                        continue;

                    var rawSize = Math.Clamp(callback.ParamSize, 0, 64);
                    var raw = new byte[rawSize];
                    if (callback.Param != 0 && raw.Length > 0)
                        Marshal.Copy(callback.Param, raw, 0, raw.Length);

                    Console.WriteLine($"ManualCallback id={callback.Callback} size={callback.ParamSize} raw={Convert.ToHexString(raw)}");
                    if (callback.Callback == callbackId && raw.Length >= callbackSize)
                    {
                        var callbackRaw = raw[..callbackSize];
                        Console.WriteLine($"ManualCallbackResult callback={callbackId} raw={Convert.ToHexString(callbackRaw)}");
                        return new ApiCallResult(true, false, callbackRaw);
                    }

                    if (callback.Callback != SteamApiCallCompletedCallback || raw.Length < 16)
                        continue;

                    var completedCall = BitConverter.ToUInt64(raw, 0);
                    var expectedCallback = BitConverter.ToInt32(raw, 8);
                    var expectedSize = BitConverter.ToInt32(raw, 12);
                    Console.WriteLine($"APICallCompleted call={completedCall} expected_callback={expectedCallback} expected_size={expectedSize} failure_reason={api.GetApiCallFailureReason(utils, completedCall)}");
                    if (completedCall != call)
                        continue;

                    var resultBuffer = Marshal.AllocHGlobal(callbackSize);
                    try
                    {
                        var failed = (byte)0;
                        var resultOk = api.ManualDispatchGetApiCallResult(pipe, call, resultBuffer, callbackSize, callbackId, ref failed);
                        var resultRaw = new byte[callbackSize];
                        Marshal.Copy(resultBuffer, resultRaw, 0, resultRaw.Length);
                        Console.WriteLine($"ManualCallResult callback={callbackId} ok={resultOk} failed={failed} raw={Convert.ToHexString(resultRaw)}");
                        failedCallResult = new ApiCallResult(resultOk != 0, failed != 0, resultRaw);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(resultBuffer);
                    }
                }
                finally
                {
                    api.ManualDispatchFreeLastCallback(pipe);
                }
            }

            Thread.Sleep(100);
        }

        return failedCallResult ?? new ApiCallResult(false, false, Array.Empty<byte>());
    }

    private static LobbyCreatedResult DecodeLobbyCreated(ApiCallResult callResult)
    {
        var result = callResult.Raw.Length >= 4 ? BitConverter.ToInt32(callResult.Raw, 0) : 0;
        var lobbyIdOffset = callResult.Raw.Length >= LobbyCreatedPaddedPayloadSize
            ? sizeof(int) * 2
            : sizeof(int);
        var lobbyId = callResult.Raw.Length >= lobbyIdOffset + sizeof(ulong)
            ? BitConverter.ToUInt64(callResult.Raw, lobbyIdOffset)
            : 0;
        Console.WriteLine($"LobbyCreated result={result} lobby_id={lobbyId} callback_ok={callResult.Ok && !callResult.Failed}");
        return new LobbyCreatedResult(result, lobbyId, callResult.Ok && !callResult.Failed);
    }

    private static int ParseLobbyType(string value) => value.ToLowerInvariant() switch
    {
        "private" => LobbyTypePrivate,
        "friends" or "friendsonly" => LobbyTypeFriendsOnly,
        "public" => LobbyTypePublic,
        "invisible" => LobbyTypeInvisible,
        _ => int.Parse(value),
    };

    private static string ParseGameState(string value) => value.ToLowerInvariant() switch
    {
        "lobby" => "lobby",
        "game" => "game",
        _ => throw new ArgumentException("Game state must be either 'lobby' or 'game'.", nameof(value)),
    };

    private readonly record struct LobbyCreatedResult(int Result, ulong LobbyId, bool CallbackOk);
    private readonly record struct ApiCallResult(bool Ok, bool Failed, byte[] Raw);
    private readonly record struct ServerEndpoint(
        string ConnectString,
        IPAddress Address,
        uint HostOrderIp,
        ushort Port)
    {
        internal IPEndPoint ToIPEndPoint() => new(Address, Port);
    }

    private enum LobbySettingsProfile
    {
        Legacy,
        Real,
    }

    private readonly record struct ServerLobbyContext(
        ulong LobbyId,
        ulong OwnerSteamId,
        ServerEndpoint Endpoint,
        string GameState,
        LobbySettingsProfile SettingsProfile);

    [StructLayout(LayoutKind.Sequential)]
    private struct CallbackMsg
    {
        public int SteamUser;
        public int Callback;
        public nint Param;
        public int ParamSize;
    }

    private sealed class SteamApi
    {
        private readonly SteamApiInit? _init;
        private readonly SteamApiInitFlat? _initFlat;
        private readonly SteamApiShutdown _shutdown;
        private readonly SteamApiRunCallbacks _runCallbacks;
        private readonly SteamApiGetHSteamPipe _getHSteamPipe;
        private readonly SteamApiManualDispatchInit _manualDispatchInit;
        private readonly SteamApiManualDispatchRunFrame _manualDispatchRunFrame;
        private readonly SteamApiManualDispatchGetNextCallback _manualDispatchGetNextCallback;
        private readonly SteamApiManualDispatchFreeLastCallback _manualDispatchFreeLastCallback;
        private readonly SteamApiManualDispatchGetApiCallResult _manualDispatchGetApiCallResult;
        private readonly SteamApiSteamMatchmaking? _steamMatchmaking;
        private readonly SteamApiSteamUtils? _steamUtils;
        private readonly SteamApiSteamUser? _steamUser;
        private readonly SteamApiGetHSteamUser? _getHSteamUser;
        private readonly SteamInternalFindOrCreateUserInterface? _findOrCreateUserInterface;
        private readonly SteamApiUtilsAppId _getUtilsAppId;
        private readonly SteamApiUserLoggedOn _isLoggedOn;
        private readonly SteamApiUserSteamId _getSteamId;
        private readonly SteamApiRequestLobbyList _requestLobbyList;
        private readonly SteamApiCreateLobby _createLobby;
        private readonly SteamApiJoinLobby _joinLobby;
        private readonly SteamApiIsApiCallCompleted _isApiCallCompleted;
        private readonly SteamApiGetApiCallResult _getApiCallResult;
        private readonly SteamApiGetApiCallFailureReason _getApiCallFailureReason;
        private readonly SteamApiRequestLobbyData _requestLobbyData;
        private readonly SteamApiSetLobbyData _setLobbyData;
        private readonly SteamApiGetLobbyData _getLobbyData;
        private readonly SteamApiGetLobbyOwner _getLobbyOwner;
        private readonly SteamApiGetNumLobbyMembers _getNumLobbyMembers;
        private readonly SteamApiGetLobbyMemberByIndex _getLobbyMemberByIndex;
        private readonly SteamApiSetLobbyOwner _setLobbyOwner;
        private readonly SteamApiSetLobbyGameServer _setLobbyGameServer;
        private readonly SteamApiGetLobbyGameServer _getLobbyGameServer;
        private readonly SteamApiGetLobbyChatEntry _getLobbyChatEntry;
        private readonly SteamApiSendLobbyChatMsg _sendLobbyChatMsg;
        private readonly SteamApiLeaveLobby _leaveLobby;

        private SteamApi(nint module)
        {
            _init = TryGet<SteamApiInit>(module, "SteamAPI_Init");
            _initFlat = _init is null ? Get<SteamApiInitFlat>(module, "SteamAPI_InitFlat") : null;
            _shutdown = Get<SteamApiShutdown>(module, "SteamAPI_Shutdown");
            _runCallbacks = Get<SteamApiRunCallbacks>(module, "SteamAPI_RunCallbacks");
            _getHSteamPipe = Get<SteamApiGetHSteamPipe>(module, "SteamAPI_GetHSteamPipe");
            _manualDispatchInit = Get<SteamApiManualDispatchInit>(module, "SteamAPI_ManualDispatch_Init");
            _manualDispatchRunFrame = Get<SteamApiManualDispatchRunFrame>(module, "SteamAPI_ManualDispatch_RunFrame");
            _manualDispatchGetNextCallback = Get<SteamApiManualDispatchGetNextCallback>(module, "SteamAPI_ManualDispatch_GetNextCallback");
            _manualDispatchFreeLastCallback = Get<SteamApiManualDispatchFreeLastCallback>(module, "SteamAPI_ManualDispatch_FreeLastCallback");
            _manualDispatchGetApiCallResult = Get<SteamApiManualDispatchGetApiCallResult>(module, "SteamAPI_ManualDispatch_GetAPICallResult");
            _steamMatchmaking = TryGet<SteamApiSteamMatchmaking>(module, "SteamAPI_SteamMatchmaking_v009");
            _steamUtils = TryGet<SteamApiSteamUtils>(module, "SteamAPI_SteamUtils_v010", "SteamAPI_SteamUtils_v011");
            _steamUser = TryGet<SteamApiSteamUser>(module, "SteamAPI_SteamUser_v021", "SteamAPI_SteamUser_v023");
            _getHSteamUser = null;
            _findOrCreateUserInterface = null;
            if (_steamMatchmaking is null || _steamUtils is null || _steamUser is null)
            {
                _getHSteamUser = Get<SteamApiGetHSteamUser>(module, "SteamAPI_GetHSteamUser");
                _findOrCreateUserInterface = Get<SteamInternalFindOrCreateUserInterface>(module, "SteamInternal_FindOrCreateUserInterface");
            }
            _getUtilsAppId = Get<SteamApiUtilsAppId>(module, "SteamAPI_ISteamUtils_GetAppID");
            _isLoggedOn = Get<SteamApiUserLoggedOn>(module, "SteamAPI_ISteamUser_BLoggedOn");
            _getSteamId = Get<SteamApiUserSteamId>(module, "SteamAPI_ISteamUser_GetSteamID");
            _requestLobbyList = Get<SteamApiRequestLobbyList>(module, "SteamAPI_ISteamMatchmaking_RequestLobbyList");
            _createLobby = Get<SteamApiCreateLobby>(module, "SteamAPI_ISteamMatchmaking_CreateLobby");
            _joinLobby = Get<SteamApiJoinLobby>(module, "SteamAPI_ISteamMatchmaking_JoinLobby");
            _isApiCallCompleted = Get<SteamApiIsApiCallCompleted>(module, "SteamAPI_ISteamUtils_IsAPICallCompleted");
            _getApiCallResult = Get<SteamApiGetApiCallResult>(module, "SteamAPI_ISteamUtils_GetAPICallResult");
            _getApiCallFailureReason = Get<SteamApiGetApiCallFailureReason>(module, "SteamAPI_ISteamUtils_GetAPICallFailureReason");
            _requestLobbyData = Get<SteamApiRequestLobbyData>(module, "SteamAPI_ISteamMatchmaking_RequestLobbyData");
            _setLobbyData = Get<SteamApiSetLobbyData>(module, "SteamAPI_ISteamMatchmaking_SetLobbyData");
            _getLobbyData = Get<SteamApiGetLobbyData>(module, "SteamAPI_ISteamMatchmaking_GetLobbyData");
            _getLobbyOwner = Get<SteamApiGetLobbyOwner>(module, "SteamAPI_ISteamMatchmaking_GetLobbyOwner");
            _getNumLobbyMembers = Get<SteamApiGetNumLobbyMembers>(module, "SteamAPI_ISteamMatchmaking_GetNumLobbyMembers");
            _getLobbyMemberByIndex = Get<SteamApiGetLobbyMemberByIndex>(module, "SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex");
            _setLobbyOwner = Get<SteamApiSetLobbyOwner>(module, "SteamAPI_ISteamMatchmaking_SetLobbyOwner");
            _setLobbyGameServer = Get<SteamApiSetLobbyGameServer>(module, "SteamAPI_ISteamMatchmaking_SetLobbyGameServer");
            _getLobbyGameServer = Get<SteamApiGetLobbyGameServer>(module, "SteamAPI_ISteamMatchmaking_GetLobbyGameServer");
            _getLobbyChatEntry = Get<SteamApiGetLobbyChatEntry>(module, "SteamAPI_ISteamMatchmaking_GetLobbyChatEntry");
            _sendLobbyChatMsg = Get<SteamApiSendLobbyChatMsg>(module, "SteamAPI_ISteamMatchmaking_SendLobbyChatMsg");
            _leaveLobby = Get<SteamApiLeaveLobby>(module, "SteamAPI_ISteamMatchmaking_LeaveLobby");
        }

        public static SteamApi Load(nint module) => new(module);
        public bool Init()
        {
            if (_init is not null)
                return _init() != 0;

            var errorMessage = Marshal.AllocHGlobal(1024);
            try
            {
                return _initFlat!(errorMessage) == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(errorMessage);
            }
        }
        public void Shutdown() => _shutdown();
        public void RunCallbacks() => _runCallbacks();
        public void ManualDispatchInit() => _manualDispatchInit();
        public int GetHSteamPipe() => _getHSteamPipe();
        public void ManualDispatchRunFrame(int pipe) => _manualDispatchRunFrame(pipe);
        public bool ManualDispatchGetNextCallback(int pipe, ref CallbackMsg callback) => _manualDispatchGetNextCallback(pipe, ref callback) != 0;
        public void ManualDispatchFreeLastCallback(int pipe) => _manualDispatchFreeLastCallback(pipe);
        public byte ManualDispatchGetApiCallResult(int pipe, ulong call, nint callback, int callbackSize, int callbackId, ref byte failed) =>
            _manualDispatchGetApiCallResult(pipe, call, callback, callbackSize, callbackId, ref failed);
        public nint SteamMatchmaking() => _steamMatchmaking?.Invoke() ?? FindOrCreateUserInterface("SteamMatchMaking009");
        public nint SteamUtils() => _steamUtils?.Invoke() ?? FindOrCreateUserInterface("SteamUtils011");
        public nint SteamUser() => _steamUser?.Invoke() ?? FindOrCreateUserInterface("SteamUser023");
        public uint GetUtilsAppId() => _getUtilsAppId(SteamUtils());
        public bool IsLoggedOn(nint self) => _isLoggedOn(self) != 0;
        public ulong GetSteamId(nint self) => _getSteamId(self);
        public ulong RequestLobbyList(nint self) => _requestLobbyList(self);
        public ulong CreateLobby(nint self, int type, int maxMembers) => _createLobby(self, type, maxMembers);
        public ulong JoinLobby(nint self, ulong lobbyId) => _joinLobby(self, lobbyId);
        public bool IsApiCallCompleted(nint utils, ulong call, ref byte failed) => _isApiCallCompleted(utils, call, ref failed) != 0;
        public byte GetApiCallResult(nint utils, ulong call, nint callback, int callbackSize, int callbackId, ref byte failed) =>
            _getApiCallResult(utils, call, callback, callbackSize, callbackId, ref failed);
        public int GetApiCallFailureReason(nint utils, ulong call) => _getApiCallFailureReason(utils, call);
        public bool RequestLobbyData(nint self, ulong lobbyId) => _requestLobbyData(self, lobbyId) != 0;
        public bool SetLobbyData(nint self, ulong lobbyId, string key, string value) => _setLobbyData(self, lobbyId, key, value) != 0;
        public ulong GetLobbyOwner(nint self, ulong lobbyId) => _getLobbyOwner(self, lobbyId);
        public int GetNumLobbyMembers(nint self, ulong lobbyId) => _getNumLobbyMembers(self, lobbyId);
        public ulong GetLobbyMemberByIndex(nint self, ulong lobbyId, int memberIndex) =>
            _getLobbyMemberByIndex(self, lobbyId, memberIndex);
        public bool SetLobbyOwner(nint self, ulong lobbyId, ulong newOwnerSteamId) =>
            _setLobbyOwner(self, lobbyId, newOwnerSteamId) != 0;
        public void SetLobbyGameServer(nint self, ulong lobbyId, uint ip, ushort port, ulong serverId) =>
            _setLobbyGameServer(self, lobbyId, ip, port, serverId);
        public bool GetLobbyGameServer(nint self, ulong lobbyId, out uint ip, out ushort port, out ulong serverId) =>
            _getLobbyGameServer(self, lobbyId, out ip, out port, out serverId) != 0;
        public int GetLobbyChatEntry(
            nint self,
            ulong lobbyId,
            int chatId,
            out ulong senderUserId,
            nint data,
            int dataSize,
            out int entryType) =>
            _getLobbyChatEntry(self, lobbyId, chatId, out senderUserId, data, dataSize, out entryType);
        public bool SendLobbyChatMsg(nint self, ulong lobbyId, byte[] data)
        {
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return _sendLobbyChatMsg(self, lobbyId, handle.AddrOfPinnedObject(), data.Length) != 0;
            }
            finally
            {
                handle.Free();
            }
        }

        public string GetLobbyData(nint self, ulong lobbyId, string key)
        {
            var ptr = _getLobbyData(self, lobbyId, key);
            return ptr == 0 ? "<null>" : Marshal.PtrToStringAnsi(ptr) ?? "<empty>";
        }

        public void LeaveLobby(nint self, ulong lobbyId) => _leaveLobby(self, lobbyId);

        private nint FindOrCreateUserInterface(string version)
        {
            var result = _findOrCreateUserInterface!(_getHSteamUser!(), version);
            if (result == 0)
                throw new InvalidOperationException($"Steam did not provide interface {version}.");

            return result;
        }

        private static T Get<T>(nint module, params string[] names) where T : Delegate =>
            TryGet<T>(module, names) ?? throw new EntryPointNotFoundException($"None of the expected exports were found: {string.Join(", ", names)}");

        private static T? TryGet<T>(nint module, params string[] names) where T : Delegate
        {
            foreach (var name in names)
            {
                if (NativeLibrary.TryGetExport(module, name, out var export))
                    return Marshal.GetDelegateForFunctionPointer<T>(export);
            }

            return null;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiInit();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SteamApiInitFlat(nint errorMessage);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiShutdown();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiRunCallbacks();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SteamApiGetHSteamPipe();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SteamApiGetHSteamUser();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SteamInternalFindOrCreateUserInterface(int user, [MarshalAs(UnmanagedType.LPStr)] string version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiManualDispatchInit();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiManualDispatchRunFrame(int pipe);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiManualDispatchGetNextCallback(int pipe, ref CallbackMsg callback);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiManualDispatchFreeLastCallback(int pipe);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiManualDispatchGetApiCallResult(int pipe, ulong call, nint callback, int callbackSize, int callbackId, ref byte failed);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SteamApiSteamMatchmaking();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SteamApiSteamUtils();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SteamApiSteamUser();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint SteamApiUtilsAppId(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiUserLoggedOn(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamApiUserSteamId(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamApiRequestLobbyList(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamApiCreateLobby(nint self, int lobbyType, int maxMembers);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamApiJoinLobby(nint self, ulong lobbyId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiIsApiCallCompleted(nint self, ulong call, ref byte failed);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiGetApiCallResult(nint self, ulong call, nint callback, int callbackSize, int callbackId, ref byte failed);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SteamApiGetApiCallFailureReason(nint self, ulong call);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiRequestLobbyData(nint self, ulong lobbyId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiSetLobbyData(nint self, ulong lobbyId, [MarshalAs(UnmanagedType.LPStr)] string key, [MarshalAs(UnmanagedType.LPStr)] string value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint SteamApiGetLobbyData(nint self, ulong lobbyId, [MarshalAs(UnmanagedType.LPStr)] string key);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamApiGetLobbyOwner(nint self, ulong lobbyId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SteamApiGetNumLobbyMembers(nint self, ulong lobbyId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong SteamApiGetLobbyMemberByIndex(nint self, ulong lobbyId, int memberIndex);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiSetLobbyOwner(nint self, ulong lobbyId, ulong newOwnerSteamId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiSetLobbyGameServer(nint self, ulong lobbyId, uint ip, ushort port, ulong serverId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiGetLobbyGameServer(nint self, ulong lobbyId, out uint ip, out ushort port, out ulong serverId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SteamApiGetLobbyChatEntry(nint self, ulong lobbyId, int chatId, out ulong senderUserId, nint data, int dataSize, out int entryType);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte SteamApiSendLobbyChatMsg(nint self, ulong lobbyId, nint data, int dataSize);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SteamApiLeaveLobby(nint self, ulong lobbyId);
}
