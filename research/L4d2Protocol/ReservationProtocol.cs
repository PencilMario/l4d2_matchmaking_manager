using System.Buffers.Binary;
using System.Text;

internal static class ReservationProtocol
{
    internal const byte ChallengeRequestType = 0x71;
    internal const byte ChallengeResponseType = 0x41;
    internal const byte ReservationRequestType = 0x6e;
    internal const byte ReservationResponseType = 0x70;
    internal const int DefaultHostVersion = 2243;

    private const uint ConnectionlessHeader = 0xffffffff;
    private const uint PayloadMagic = 0xfeedbeef;
    private const uint IceKeyLeftXor = 0x5ef8ce12;
    private const uint IceKeyRightXor = 0xaa98e42c;
    private static readonly byte[] ChallengeContext = Encoding.ASCII.GetBytes("reserve0000000\0");

    internal static byte[] CreateChallengeRequest()
    {
        var request = new byte[5 + ChallengeContext.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(request, ConnectionlessHeader);
        request[4] = ChallengeRequestType;
        ChallengeContext.CopyTo(request, 5);
        return request;
    }

    internal static bool TryParseChallengeResponse(
        ReadOnlySpan<byte> response,
        out ReservationChallenge challenge)
    {
        challenge = default;
        if (response.Length < 25
            || BinaryPrimitives.ReadUInt32LittleEndian(response) != ConnectionlessHeader
            || response[4] != ChallengeResponseType)
        {
            return false;
        }

        var contextBytes = response[24..];
        var terminator = contextBytes.IndexOf((byte)0);
        if (terminator < 0)
            return false;

        var context = Encoding.ASCII.GetString(contextBytes[..terminator]);
        if (!context.StartsWith("reserve", StringComparison.Ordinal))
            return false;

        challenge = new ReservationChallenge(
            BinaryPrimitives.ReadUInt32LittleEndian(response[5..]),
            BinaryPrimitives.ReadInt32LittleEndian(response[9..]),
            BinaryPrimitives.ReadUInt64LittleEndian(response[15..]),
            response[23] != 0,
            context);
        return true;
    }

    internal static ReservationPacket CreateReservationRequest(
        int hostVersion,
        uint challenge,
        ulong cookie,
        ReadOnlySpan<byte> settings)
    {
        if (hostVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(hostVersion));
        if (cookie == 0)
            throw new ArgumentOutOfRangeException(nameof(cookie));

        var unpaddedLength = 16 + settings.Length;
        var payloadLength = (unpaddedLength + 7) & ~7;
        if (payloadLength > 1024)
            throw new ArgumentException("Reservation payload must not exceed 1024 bytes.", nameof(settings));

        var plaintext = new byte[payloadLength];
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext, PayloadMagic);
        BinaryPrimitives.WriteUInt64LittleEndian(plaintext.AsSpan(4), cookie);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(12), settings.Length);
        settings.CopyTo(plaintext.AsSpan(16));

        var iceKey = CreateIceKey(challenge);
        var encrypted = new IceCipher(iceKey).Encrypt(plaintext);
        var request = new byte[13 + encrypted.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(request, ConnectionlessHeader);
        request[4] = ReservationRequestType;
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(5), hostVersion);
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(9), encrypted.Length);
        encrypted.CopyTo(request, 13);

        return new ReservationPacket(iceKey, plaintext, encrypted, request);
    }

    internal static bool TryParseReservationResponse(
        ReadOnlySpan<byte> response,
        out ReservationResponse reservationResponse)
    {
        reservationResponse = default;
        if (response.Length != 10
            || BinaryPrimitives.ReadUInt32LittleEndian(response) != ConnectionlessHeader
            || response[4] != ReservationResponseType)
        {
            return false;
        }

        reservationResponse = new ReservationResponse(
            BinaryPrimitives.ReadInt32LittleEndian(response[5..]),
            (response[9] & 1) != 0);
        return true;
    }

    private static byte[] CreateIceKey(uint challenge)
    {
        var key = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(key, challenge ^ IceKeyLeftXor);
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(4), challenge ^ IceKeyRightXor);
        return key;
    }
}

internal readonly record struct ReservationChallenge(
    uint Challenge,
    int AuthProtocol,
    ulong ServerSteamId,
    bool Secure,
    string Context);

internal readonly record struct ReservationPacket(
    byte[] IceKey,
    byte[] Plaintext,
    byte[] EncryptedPayload,
    byte[] Request);

internal readonly record struct ReservationResponse(
    int HostVersion,
    bool Accepted);
