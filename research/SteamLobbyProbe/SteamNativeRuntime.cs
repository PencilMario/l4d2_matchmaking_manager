using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using L4d2Matchmaking.Contracts;

internal sealed class SteamNativeRuntime(string steamApiLibraryPath) : ISteamNativeRuntime
{
    private const int LobbyTypePublic = 2;
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

    public AgentHealthSnapshot ObserveHealth()
    {
        try
        {
            EnsureInitialized();
            if (_api!.GetUtilsAppId() != 550)
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
            var metadata = RealSessionSettings.CreateLobbyMetadata(profile);
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
                if (reservationResult != 0)
                    throw new SteamRuntimeException("reservation_failed");
            }

            return ReadLobby(created.LobbyId);
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

        _api!.RequestLobbyData(_matchmaking, lobbyId);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            PumpCallbacks();
            Thread.Sleep(25);
        }

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

    public void LeaveLobby(ulong lobbyId)
    {
        EnsureInitialized();
        if (lobbyId != 0)
            _api!.LeaveLobby(_matchmaking, lobbyId);
    }

    public void PumpCallbacks()
    {
        if (!_initialized)
            return;

        _api!.ManualDispatchRunFrame(_pipe);
        var callback = new Program.CallbackMsg();
        while (_api.ManualDispatchGetNextCallback(_pipe, ref callback))
            _api.ManualDispatchFreeLastCallback(_pipe);
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

internal sealed class SteamRuntimeException(string code, Exception? innerException = null)
    : InvalidOperationException(code, innerException)
{
    internal string Code { get; } = code;
}
