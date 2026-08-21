using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using L4d2Matchmaking.Contracts;

internal sealed class SteamNativeRuntime(
    string steamApiLibraryPath,
    uint expectedAppId = SteamAppConfiguration.DefaultAppId) : ISteamNativeRuntime
{
    private const int LobbyTypePublic = 2;
    private const int LobbyEnterCallback = 504;
    private const int LobbyDataUpdateCallback = 505;
    private const int LobbyChatMsgCallback = 507;
    private const int LobbyCreatedCallback = 513;
    private const int LobbyMatchListCallback = 510;
    private const int SteamApiCallCompletedCallback = 703;
    private readonly string _steamApiLibraryPath = steamApiLibraryPath;
    private nint _module;
    private Program.SteamApi? _api;
    private nint _matchmaking;
    private nint _utils;
    private nint _user;
    private int _pipe;
    private bool _initialized;
    private ActiveLobbyJoinDataResponder? _activeJoinDataResponder;

    public AgentHealthSnapshot ObserveHealth()
    {
        try
        {
            EnsureInitialized();
            if (_api!.GetUtilsAppId() != expectedAppId)
                return new AgentHealthSnapshot(false, "appid_mismatch", DateTimeOffset.UtcNow);
            if (!_api.IsLoggedOn(_user) || _api.GetSteamId(_user) == 0)
                return new AgentHealthSnapshot(false, "steam_not_logged_on", DateTimeOffset.UtcNow);

            var call = _api.RequestLobbyList(_matchmaking);
            if (call == 0)
                return new AgentHealthSnapshot(false, "lobby_list_request_failed", DateTimeOffset.UtcNow);
            var result = WaitForCall(call, LobbyMatchListCallback, sizeof(uint));
            return result.Ok && !result.Failed
                ? new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow)
                : new AgentHealthSnapshot(
                    false,
                    result.Failed ? "lobby_list_callback_failed" : "lobby_list_timeout",
                    DateTimeOffset.UtcNow);
        }
        catch (SteamRuntimeException exception)
        {
            return new AgentHealthSnapshot(false, exception.Code, DateTimeOffset.UtcNow);
        }
        catch (Exception)
        {
            return new AgentHealthSnapshot(false, "steam_api_runtime_failed", DateTimeOffset.UtcNow);
        }
    }

    public LobbySnapshot CreateLobby(AgentOperationRequest request, CampaignProfile profile)
    {
        EnsureInitialized();
        if (!IPAddress.TryParse(request.Ipv4Address, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            throw new SteamRuntimeException("invalid_target_endpoint");
        if (request.Port == 0)
            throw new SteamRuntimeException("invalid_target_endpoint");

        var call = _api!.CreateLobby(_matchmaking, LobbyTypePublic, RealSessionSettings.NumSlots);
        var created = DecodeLobbyCreated(WaitForCall(call, LobbyCreatedCallback, sizeof(int) + sizeof(ulong)));
        if (!created.Ok || created.LobbyId == 0)
            throw new SteamRuntimeException("lobby_create_failed");

        try
        {
            var metadata = RealSessionSettings.CreateLobbyMetadata(profile, request.GameMode);
            foreach (var pair in CreateServerMetadata(address.ToString(), request.Port, created.LobbyId))
                metadata.Add(pair.Key, pair.Value);
            foreach (var pair in metadata)
            {
                if (!_api.SetLobbyData(_matchmaking, created.LobbyId, pair.Key, pair.Value))
                    throw new SteamRuntimeException("lobby_metadata_write_failed");
            }

            var bytes = address.GetAddressBytes();
            var hostOrderIp =
                ((uint)bytes[0] << 24) |
                ((uint)bytes[1] << 16) |
                ((uint)bytes[2] << 8) |
                bytes[3];
            _api.SetLobbyGameServer(_matchmaking, created.LobbyId, hostOrderIp, request.Port, 0);

            if (request.Mode == AgentLobbyMode.Reserved)
            {
                var reservationResult = ReservationCommand.RunLive(
                    new IPEndPoint(address, request.Port),
                    created.LobbyId,
                    RealSessionSettings.EncodeReservationSettings(profile),
                    5000,
                    ReservationProtocol.DefaultHostVersion);
                if (!ReservationResultVerifier.IsAccepted(
                        reservationResult,
                        new IPEndPoint(address, request.Port),
                        created.LobbyId,
                        request.RconPassword,
                        static (endpoint, password, lobbyId) =>
                            SourceRconClient.VerifyReservation(endpoint, password, lobbyId, TimeSpan.FromSeconds(3))))
                    throw new SteamRuntimeException("reservation_failed");
            }

            var lobby = ReadLobby(created.LobbyId);
            var ownerSteamId = _api.GetSteamId(_user);
            if (ownerSteamId == 0)
                throw new SteamRuntimeException("steam_not_logged_on");
            _activeJoinDataResponder = new ActiveLobbyJoinDataResponder(
                created.LobbyId,
                ownerSteamId,
                address + ":" + request.Port);
            return lobby;
        }
        catch
        {
            _api.LeaveLobby(_matchmaking, created.LobbyId);
            throw;
        }
    }

    public LobbySnapshot ReadLobby(ulong lobbyId)
    {
        EnsureInitialized();
        if (lobbyId == 0)
            throw new ArgumentOutOfRangeException(nameof(lobbyId));

        if (!_api!.RequestLobbyData(_matchmaking, lobbyId) || !WaitForLobbyDataUpdate(lobbyId))
            throw new SteamRuntimeException("lobby_data_unavailable");

        var memberCount = Math.Max(0, _api.GetNumLobbyMembers(_matchmaking, lobbyId));
        var members = new List<LobbyMemberSnapshot>(memberCount);
        for (var index = 0; index < memberCount; index++)
        {
            var steamId = _api.GetLobbyMemberByIndex(_matchmaking, lobbyId, index);
            if (steamId != 0)
                members.Add(new LobbyMemberSnapshot(steamId.ToString(), null));
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var metadataCount = Math.Max(0, _api.GetLobbyDataCount(_matchmaking, lobbyId));
        for (var index = 0; index < metadataCount; index++)
        {
            if (_api.GetLobbyDataByIndex(_matchmaking, lobbyId, index, out var key, out var value) &&
                !string.IsNullOrEmpty(key))
            {
                metadata[key] = value;
            }
        }

        return new LobbySnapshot(
            lobbyId.ToString(),
            _api.GetLobbyOwner(_matchmaking, lobbyId).ToString(),
            members,
            metadata,
            DateTimeOffset.UtcNow);
    }

    public NativeLobbyJoinResult JoinLobby(ulong lobbyId)
    {
        EnsureInitialized();
        if (lobbyId == 0)
            throw new ArgumentOutOfRangeException(nameof(lobbyId));

        var call = _api!.JoinLobby(_matchmaking, lobbyId);
        if (call == 0)
            return NativeLobbyJoinResult.Denied;

        var result = WaitForCall(call, LobbyEnterCallback, 24);
        if (!result.Ok)
            return result.Failed ? NativeLobbyJoinResult.Denied : NativeLobbyJoinResult.Timeout;

        var enteredLobbyId = result.Raw.Length >= sizeof(ulong)
            ? BitConverter.ToUInt64(result.Raw, 0)
            : 0;
        var enterResponse = result.Raw.Length >= 20
            ? BitConverter.ToUInt32(result.Raw, 16)
            : 0;
        return enteredLobbyId == lobbyId && enterResponse == 1
            ? NativeLobbyJoinResult.Success
            : NativeLobbyJoinResult.Denied;
    }

    public ulong GetCurrentSteamId()
    {
        EnsureInitialized();
        return _api!.GetSteamId(_user);
    }

    public bool IsCurrentUserLobbyMember(ulong lobbyId, ulong steamId)
    {
        EnsureInitialized();
        if (lobbyId == 0 || steamId == 0)
            return false;

        var memberCount = Math.Max(0, _api!.GetNumLobbyMembers(_matchmaking, lobbyId));
        for (var index = 0; index < memberCount; index++)
        {
            if (_api.GetLobbyMemberByIndex(_matchmaking, lobbyId, index) == steamId)
                return true;
        }

        return false;
    }

    public void LeaveLobby(ulong lobbyId)
    {
        EnsureInitialized();
        if (lobbyId != 0)
        {
            if (_activeJoinDataResponder?.LobbyId == lobbyId)
                _activeJoinDataResponder = null;
            _api!.LeaveLobby(_matchmaking, lobbyId);
        }
    }

    public void PumpCallbacks()
    {
        if (!_initialized)
            return;

        _api!.ManualDispatchRunFrame(_pipe);
        var callback = new Program.CallbackMsg();
        while (_api.ManualDispatchGetNextCallback(_pipe, ref callback))
        {
            try
            {
                if (callback.Callback == LobbyChatMsgCallback)
                    HandleLobbyChatMessage(callback);
            }
            finally
            {
                _api.ManualDispatchFreeLastCallback(_pipe);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            if (_initialized)
                _api?.Shutdown();
        }
        finally
        {
            _activeJoinDataResponder = null;
            if (_module != 0)
                NativeLibrary.Free(_module);
            _module = 0;
            _api = null;
            _initialized = false;
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;
        if (string.IsNullOrWhiteSpace(_steamApiLibraryPath) || !File.Exists(_steamApiLibraryPath))
            throw new SteamRuntimeException("steam_api_load_failed");

        try
        {
            _module = NativeLibrary.Load(Path.GetFullPath(_steamApiLibraryPath));
            _api = Program.SteamApi.Load(_module);
            if (!_api.Init())
                throw new SteamRuntimeException("steam_api_init_failed");
            _api.ManualDispatchInit();
            _matchmaking = _api.SteamMatchmaking();
            _utils = _api.SteamUtils();
            _user = _api.SteamUser();
            _pipe = _api.GetHSteamPipe();
            _initialized = true;
        }
        catch (SteamRuntimeException)
        {
            Dispose();
            throw;
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            Dispose();
            throw new SteamRuntimeException("steam_api_load_failed", exception);
        }
    }

    internal static Dictionary<string, string> CreateServerMetadata(string ipv4Address, ushort port, ulong lobbyId)
    {
        if (!IPAddress.TryParse(ipv4Address, out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
            port == 0 || lobbyId == 0)
        {
            throw new ArgumentException("A nonzero IPv4 endpoint and lobby ID are required.");
        }

        var endpoint = address + ":" + port;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["server:adrlocal"] = endpoint,
            ["server:adronline"] = endpoint,
            ["server:connectstring"] = endpoint,
            ["server:reservationid"] = lobbyId.ToString(),
        };
    }

    private void HandleLobbyChatMessage(Program.CallbackMsg callback)
    {
        if (_activeJoinDataResponder is null || callback.Param == 0 || callback.ParamSize < 24)
            return;

        var raw = new byte[Math.Clamp(callback.ParamSize, 0, 64)];
        Marshal.Copy(callback.Param, raw, 0, raw.Length);
        var lobbyId = BitConverter.ToUInt64(raw, 0);
        var chatId = BitConverter.ToInt32(raw, 20);
        var buffer = Marshal.AllocHGlobal(4096);
        try
        {
            var size = _api!.GetLobbyChatEntry(
                _matchmaking,
                lobbyId,
                chatId,
                out var senderSteamId,
                buffer,
                4096,
                out var entryType);
            if (size <= 0 || entryType != 1)
                return;

            var message = new byte[Math.Min(size, 4096)];
            Marshal.Copy(buffer, message, 0, message.Length);
            if (_activeJoinDataResponder.TryCreateReply(lobbyId, message, senderSteamId, out var reply) &&
                reply.Length > 0)
            {
                var sent = _api.SendLobbyChatMsg(_matchmaking, lobbyId, reply);
                Console.WriteLine($"ReplyJoinData lobby_id={lobbyId} recipient={senderSteamId} size={reply.Length} sent={sent}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private SteamCallResult WaitForCall(ulong call, int callbackId, int minimumSize)
    {
        if (call == 0)
            return new SteamCallResult(false, true, Array.Empty<byte>());

        SteamCallResult? completedResult = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            _api!.ManualDispatchRunFrame(_pipe);
            var callback = new Program.CallbackMsg();
            while (_api.ManualDispatchGetNextCallback(_pipe, ref callback))
            {
                try
                {
                    var size = Math.Clamp(callback.ParamSize, 0, 64);
                    var raw = new byte[size];
                    if (callback.Param != 0 && size > 0)
                        Marshal.Copy(callback.Param, raw, 0, size);

                    if (callback.Callback == callbackId && raw.Length >= minimumSize)
                        return new SteamCallResult(true, false, raw);
                    if (callback.Callback != SteamApiCallCompletedCallback || raw.Length < 16)
                        continue;
                    if (BitConverter.ToUInt64(raw, 0) != call)
                        continue;

                    var failed = (byte)0;
                    var buffer = Marshal.AllocHGlobal(minimumSize);
                    try
                    {
                        var ok = _api.ManualDispatchGetApiCallResult(
                            _pipe,
                            call,
                            buffer,
                            minimumSize,
                            callbackId,
                            ref failed);
                        var resultRaw = new byte[minimumSize];
                        Marshal.Copy(buffer, resultRaw, 0, resultRaw.Length);
                        completedResult = new SteamCallResult(ok != 0, failed != 0, resultRaw);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    _api.ManualDispatchFreeLastCallback(_pipe);
                }
            }

            Thread.Sleep(100);
        }

        return completedResult ?? new SteamCallResult(false, false, Array.Empty<byte>());
    }

    private bool WaitForLobbyDataUpdate(ulong lobbyId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            _api!.ManualDispatchRunFrame(_pipe);
            var callback = new Program.CallbackMsg();
            while (_api.ManualDispatchGetNextCallback(_pipe, ref callback))
            {
                try
                {
                    var size = Math.Clamp(callback.ParamSize, 0, 64);
                    var raw = new byte[size];
                    if (callback.Param != 0 && size > 0)
                        Marshal.Copy(callback.Param, raw, 0, size);

                    if (callback.Callback != LobbyDataUpdateCallback || raw.Length < 17)
                        continue;

                    var updatedLobbyId = BitConverter.ToUInt64(raw, 0);
                    if (updatedLobbyId == lobbyId)
                        return raw[16] != 0;
                }
                finally
                {
                    _api.ManualDispatchFreeLastCallback(_pipe);
                }
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static LobbyCreatedResult DecodeLobbyCreated(SteamCallResult result)
    {
        var code = result.Raw.Length >= sizeof(int) ? BitConverter.ToInt32(result.Raw, 0) : 0;
        var lobbyOffset = result.Raw.Length >= 16 ? sizeof(int) * 2 : sizeof(int);
        var lobbyId = result.Raw.Length >= lobbyOffset + sizeof(ulong)
            ? BitConverter.ToUInt64(result.Raw, lobbyOffset)
            : 0;
        return new LobbyCreatedResult(result.Ok && !result.Failed && code == 1, lobbyId);
    }

    private readonly record struct SteamCallResult(bool Ok, bool Failed, byte[] Raw);
    private readonly record struct LobbyCreatedResult(bool Ok, ulong LobbyId);
}

public sealed class SteamRuntimeException(string code, Exception? innerException = null)
    : InvalidOperationException(code, innerException)
{
    public string Code { get; } = code;
}
