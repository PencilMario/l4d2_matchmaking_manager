using System.Net;
using SteamKit2;

namespace L4d2AsfPlugin;

internal enum L4d2HostSessionState
{
    Creating,
    Ready,
    ReservationPending,
    ReservationResponseReceived,
    ReservationStatusRequired,
    Failed
}

internal sealed record L4d2HostSession(
    SteamID LobbyId,
    ulong OwnerSteamId,
    IPEndPoint Endpoint,
    L4d2HostSessionState State)
{
    internal bool TryCreateReply(
        SteamID chatRoomId,
        ulong senderSteamId,
        ReadOnlySpan<byte> request,
        out byte[] reply)
    {
        reply = Array.Empty<byte>();
        if (!CanReply || chatRoomId != LobbyId || senderSteamId == 0)
            return false;

        return LobbyJoinProtocol.TryCreateRealReply(
            request,
            Endpoint.ToString(),
            LobbyId.ConvertToUInt64(),
            OwnerSteamId,
            senderSteamId,
            null,
            out reply);
    }

    private bool CanReply => State is L4d2HostSessionState.Ready
        or L4d2HostSessionState.ReservationResponseReceived
        or L4d2HostSessionState.ReservationStatusRequired;
}
