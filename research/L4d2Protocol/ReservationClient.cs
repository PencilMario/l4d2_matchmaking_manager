using System.Net;
using System.Net.Sockets;

internal enum ReservationOutcome
{
    Accepted,
    Rejected,
    HostVersionMismatch,
    Timeout
}

internal readonly record struct ReservationResult(
    ReservationOutcome Outcome,
    ReservationPacket? Packet);

internal static class ReservationClient
{
    internal static async Task<ReservationResult> ReserveAsync(
        IPEndPoint endpoint,
        ulong cookie,
        byte[] settings,
        TimeSpan timeout,
        int hostVersion)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(settings);
        if (endpoint.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Reservation endpoint must be IPv4.", nameof(endpoint));
        if (cookie == 0)
            throw new ArgumentOutOfRangeException(nameof(cookie));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (hostVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(hostVersion));

        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Connect(endpoint);

        var challengeRequest = ReservationProtocol.CreateChallengeRequest();
        udp.Send(challengeRequest.AsSpan(), SocketFlags.None);
        var challengeReply = await ReceiveAsync(udp, timeout).ConfigureAwait(false);
        if (challengeReply == null)
            return new ReservationResult(ReservationOutcome.Timeout, null);
        if (!ReservationProtocol.TryParseChallengeResponse(challengeReply, out var challenge))
        {
            throw new InvalidDataException(
                $"Invalid reservation challenge: {Convert.ToHexString(challengeReply)}");
        }

        var packet = ReservationProtocol.CreateReservationRequest(
            hostVersion,
            challenge.Challenge,
            cookie,
            settings);
        udp.Send(packet.Request.AsSpan(), SocketFlags.None);
        var reservationReply = await ReceiveAsync(udp, timeout).ConfigureAwait(false);
        if (reservationReply == null)
            return new ReservationResult(ReservationOutcome.Timeout, packet);
        if (!ReservationProtocol.TryParseReservationResponse(reservationReply, out var response))
        {
            throw new InvalidDataException(
                $"Invalid reservation response: {Convert.ToHexString(reservationReply)}");
        }
        if (response.HostVersion != hostVersion)
            return new ReservationResult(ReservationOutcome.HostVersionMismatch, packet);

        return new ReservationResult(
            response.Accepted ? ReservationOutcome.Accepted : ReservationOutcome.Rejected,
            packet);
    }

    private static async Task<byte[]?> ReceiveAsync(Socket udp, TimeSpan timeout)
    {
        var buffer = new byte[ushort.MaxValue];
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            var received = await udp.ReceiveAsync(buffer, SocketFlags.None, cancellation.Token).ConfigureAwait(false);
            return buffer[..received];
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
    }
}
