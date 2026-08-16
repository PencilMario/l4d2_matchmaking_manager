using System.Globalization;
using System.Net;
using System.Net.Sockets;

internal static class ReservationCommand
{
    private const string SampleChallengeResponse =
        "FFFFFFFF41C40B09030300000000001730FEDE21C7400101726573657276653030303030303000";
    private const string SampleReservationResponse = "FFFFFFFF70C308000001";

    internal static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
            return false;

        if (string.Equals(args[0], "reservation-vector", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = RunWithErrors(() => RunVector(args));
            return true;
        }

        if (string.Equals(args[0], "real-settings-vector", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = RunWithErrors(() => RunRealSettingsVector(args));
            return true;
        }

        if (string.Equals(args[0], "reserve-server", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = RunWithErrors(() => RunLiveCommand(args));
            return true;
        }

        return false;
    }

    private static int RunRealSettingsVector(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException(
                "Usage: real-settings-vector <challenge> <cookie> <hostVersion>.");
        }

        var challenge = ParseUInt32(args[1], "challenge");
        var cookie = ParseUInt64(args[2], "cookie");
        if (!int.TryParse(
                args[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var hostVersion)
            || hostVersion <= 0)
        {
            throw new ArgumentException("Invalid hostVersion value.", "hostVersion");
        }

        var settings = RealSessionSettings.EncodeReservationSettings();
        var reservation = ReservationProtocol.CreateReservationRequest(
            hostVersion,
            challenge,
            cookie,
            settings);
        var metadata = RealSessionSettings.CreateLobbyMetadata();
        Console.WriteLine($"SettingsSize={settings.Length}");
        Console.WriteLine($"SettingsRaw={Convert.ToHexString(settings)}");
        Console.WriteLine($"IceKey={Convert.ToHexString(reservation.IceKey)}");
        Console.WriteLine($"PlainPayload={Convert.ToHexString(reservation.Plaintext)}");
        Console.WriteLine($"EncryptedPayload={Convert.ToHexString(reservation.EncryptedPayload)}");
        Console.WriteLine($"ReservationRequest={Convert.ToHexString(reservation.Request)}");
        Console.WriteLine($"LobbyMetadataCount={metadata.Count}");
        foreach (var pair in metadata)
            Console.WriteLine($"LobbyMetadata key={pair.Key} value={pair.Value}");
        return 0;
    }

    private static int RunVector(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("Usage: reservation-vector <challenge> <cookie> <hostVersion>.");

        var challenge = ParseUInt32(args[1], "challenge");
        var cookie = ParseUInt64(args[2], "cookie");
        var hostVersion = int.Parse(args[3], CultureInfo.InvariantCulture);
        var challengeRequest = ReservationProtocol.CreateChallengeRequest();
        var challengeResponseBytes = Convert.FromHexString(SampleChallengeResponse);
        if (!ReservationProtocol.TryParseChallengeResponse(challengeResponseBytes, out var challengeResponse))
            throw new InvalidOperationException("Built-in challenge response fixture is invalid.");

        var reservation = ReservationProtocol.CreateReservationRequest(hostVersion, challenge, cookie, []);
        var responseBytes = Convert.FromHexString(SampleReservationResponse);
        if (!ReservationProtocol.TryParseReservationResponse(responseBytes, out var reservationResponse))
            throw new InvalidOperationException("Built-in reservation response fixture is invalid.");

        Console.WriteLine($"ChallengeRequest={Convert.ToHexString(challengeRequest)}");
        Console.WriteLine(
            $"ChallengeResponse challenge=0x{challengeResponse.Challenge:X8} " +
            $"auth_protocol={challengeResponse.AuthProtocol} " +
            $"server_steam_id=0x{challengeResponse.ServerSteamId:X16} " +
            $"secure={challengeResponse.Secure} context={challengeResponse.Context}");
        Console.WriteLine($"IceKey={Convert.ToHexString(reservation.IceKey)}");
        Console.WriteLine($"PlainPayload={Convert.ToHexString(reservation.Plaintext)}");
        Console.WriteLine($"EncryptedPayload={Convert.ToHexString(reservation.EncryptedPayload)}");
        Console.WriteLine($"ReservationRequest={Convert.ToHexString(reservation.Request)}");
        Console.WriteLine(
            $"ReservationResponse host_version={reservationResponse.HostVersion} " +
            $"accepted={reservationResponse.Accepted}");
        return 0;
    }

    private static int RunLiveCommand(string[] args)
    {
        if (args.Length < 3 || args.Length > 5)
        {
            throw new ArgumentException(
                "Usage: reserve-server <ip:port> <cookie> [timeoutMilliseconds] [hostVersion].");
        }

        var endpoint = ParseEndpoint(args[1]);
        var cookie = ParseUInt64(args[2], "cookie");
        var timeoutMilliseconds = args.Length > 3
            ? int.Parse(args[3], CultureInfo.InvariantCulture)
            : 5000;
        var hostVersion = args.Length > 4
            ? int.Parse(args[4], CultureInfo.InvariantCulture)
            : ReservationProtocol.DefaultHostVersion;
        if (timeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));

        return RunLive(endpoint, cookie, [], timeoutMilliseconds, hostVersion);
    }

    internal static int RunLive(
        IPEndPoint endpoint,
        ulong cookie,
        ReadOnlySpan<byte> settings,
        int timeoutMilliseconds,
        int hostVersion)
    {
        if (endpoint.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Reservation endpoint must be IPv4.", nameof(endpoint));
        if (cookie == 0)
            throw new ArgumentOutOfRangeException(nameof(cookie));
        if (timeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        if (hostVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(hostVersion));

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ReceiveTimeout = timeoutMilliseconds;
        udp.Client.SendTimeout = timeoutMilliseconds;
        udp.Connect(endpoint);

        var challengeRequest = ReservationProtocol.CreateChallengeRequest();
        Console.WriteLine(
            $"ChallengeRequest endpoint={endpoint} size={challengeRequest.Length} " +
            $"raw={Convert.ToHexString(challengeRequest)}");
        udp.Send(challengeRequest, challengeRequest.Length);

        IPEndPoint remote = new(IPAddress.Any, 0);
        var challengeBytes = udp.Receive(ref remote);
        if (!ReservationProtocol.TryParseChallengeResponse(challengeBytes, out var challenge))
        {
            throw new InvalidDataException(
                $"Invalid challenge response from {remote}: {Convert.ToHexString(challengeBytes)}");
        }

        Console.WriteLine(
            $"ChallengeResponse endpoint={remote} challenge=0x{challenge.Challenge:X8} " +
            $"auth_protocol={challenge.AuthProtocol} server_steam_id=0x{challenge.ServerSteamId:X16} " +
            $"secure={challenge.Secure} context={challenge.Context} raw={Convert.ToHexString(challengeBytes)}");

        var reservation = ReservationProtocol.CreateReservationRequest(
            hostVersion,
            challenge.Challenge,
            cookie,
            settings);
        Console.WriteLine(
            $"ReservationRequest host_version={hostVersion} cookie=0x{cookie:X16} " +
            $"payload_size={reservation.EncryptedPayload.Length} raw={Convert.ToHexString(reservation.Request)}");
        udp.Send(reservation.Request, reservation.Request.Length);

        remote = new IPEndPoint(IPAddress.Any, 0);
        byte[] responseBytes;
        try
        {
            responseBytes = udp.Receive(ref remote);
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
        {
            Console.WriteLine(
                "ReservationResponse timeout=True request_sent=True " +
                "verification=server_status_required");
            return 8;
        }

        if (!ReservationProtocol.TryParseReservationResponse(responseBytes, out var response))
        {
            throw new InvalidDataException(
                $"Invalid reservation response from {remote}: {Convert.ToHexString(responseBytes)}");
        }

        Console.WriteLine(
            $"ReservationResponse endpoint={remote} host_version={response.HostVersion} " +
            $"accepted={response.Accepted} raw={Convert.ToHexString(responseBytes)}");

        if (response.HostVersion != hostVersion)
            return 6;
        return response.Accepted ? 0 : 7;
    }

    private static int RunWithErrors(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ReservationError type={exception.GetType().Name} message={exception.Message}");
            return 2;
        }
    }

    private static IPEndPoint ParseEndpoint(string value)
    {
        var separator = value.LastIndexOf(':');
        if (separator <= 0
            || !IPAddress.TryParse(value[..separator], out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(value[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            throw new ArgumentException("Server endpoint must be an IPv4 address in the form ip:port.", nameof(value));
        }

        return new IPEndPoint(address, port);
    }

    private static uint ParseUInt32(string value, string name)
    {
        var style = NumberStyles.None;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
            style = NumberStyles.AllowHexSpecifier;
        }

        if (!uint.TryParse(value, style, CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"Invalid {name} value.", name);
        return result;
    }

    private static ulong ParseUInt64(string value, string name)
    {
        var style = NumberStyles.None;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
            style = NumberStyles.AllowHexSpecifier;
        }

        if (!ulong.TryParse(value, style, CultureInfo.InvariantCulture, out var result) || result == 0)
            throw new ArgumentException($"Invalid {name} value.", name);
        return result;
    }
}
