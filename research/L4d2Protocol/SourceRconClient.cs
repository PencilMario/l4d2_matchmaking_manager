using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

internal static class SourceRconClient
{
    private const int AuthenticationType = 3;
    private const int CommandType = 2;
    private const int ResponseValueType = 0;
    private const int AuthenticationResponseType = 2;
    private const int MaximumPacketLength = 4096;
    private const int AuthenticationRequestId = 1;
    private const int StatusRequestId = 2;

    internal static bool VerifyReservation(
        IPEndPoint endpoint,
        string password,
        ulong lobbyId,
        TimeSpan timeout)
    {
        if (string.IsNullOrEmpty(password) || lobbyId == 0 || timeout <= TimeSpan.Zero)
            return false;

        try
        {
            using var client = new TcpClient(endpoint.AddressFamily);
            var connect = client.ConnectAsync(endpoint.Address, endpoint.Port);
            if (!connect.Wait(timeout))
                return false;
            connect.GetAwaiter().GetResult();
            client.ReceiveTimeout = checked((int)Math.Clamp(timeout.TotalMilliseconds, 1, int.MaxValue));
            client.SendTimeout = client.ReceiveTimeout;
            using var stream = client.GetStream();

            WritePacket(stream, AuthenticationRequestId, AuthenticationType, password);
            if (!ReadAuthentication(stream, AuthenticationRequestId))
                return false;

            WritePacket(stream, StatusRequestId, CommandType, "status");
            var status = ReadStatus(stream, StatusRequestId);
            return ContainsReservationCookie(status, lobbyId);
        }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or ArgumentException)
        {
            return false;
        }
    }

    private static bool ReadAuthentication(NetworkStream stream, int requestId)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = ReadPacket(stream);
            if (response.Id == -1)
                return false;
            if (response.Id == requestId && response.Type == AuthenticationResponseType)
                return true;
        }

        return false;
    }

    private static string ReadStatus(NetworkStream stream, int requestId)
    {
        var output = new StringBuilder();
        for (var attempt = 0; attempt < 32; attempt++)
        {
            try
            {
                var response = ReadPacket(stream);
                if (response.Id == requestId && response.Type == ResponseValueType)
                    output.Append(response.Body);
            }
            catch (IOException)
            {
                break;
            }
        }

        return output.ToString();
    }

    private static bool ContainsReservationCookie(string status, ulong lobbyId)
    {
        var cookie = lobbyId.ToString("x", CultureInfo.InvariantCulture);
        return Regex.IsMatch(
            status,
            $@"(?<![0-9a-f])reserved\s+{Regex.Escape(cookie)}(?![0-9a-f])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void WritePacket(NetworkStream stream, int id, int type, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var payload = new byte[sizeof(int) * 2 + bodyBytes.Length + 2];
        BinaryPrimitives.WriteInt32LittleEndian(payload, id);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), type);
        bodyBytes.CopyTo(payload.AsSpan(sizeof(int) * 2));
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
        stream.Write(length);
        stream.Write(payload);
        stream.Flush();
    }

    private static RconPacket ReadPacket(NetworkStream stream)
    {
        Span<byte> lengthBuffer = stackalloc byte[sizeof(int)];
        stream.ReadExactly(lengthBuffer);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length is < 10 or > MaximumPacketLength)
            throw new InvalidDataException("invalid_rcon_packet_length");

        var payload = new byte[length];
        stream.ReadExactly(payload);
        return new RconPacket(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int))),
            Encoding.UTF8.GetString(payload, sizeof(int) * 2, payload.Length - (sizeof(int) * 2 + 2)));
    }

    private sealed record RconPacket(int Id, int Type, string Body);
}
