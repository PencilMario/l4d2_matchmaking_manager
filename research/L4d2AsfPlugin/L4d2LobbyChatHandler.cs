using SteamKit2;
using SteamKit2.Internal;

namespace L4d2AsfPlugin;

internal sealed class L4d2LobbyChatHandler : ClientMsgHandler
{
    private readonly Func<L4d2HostSession?> getActiveSession;
    private readonly Action<L4d2LobbyChatAudit> audit;

    internal L4d2LobbyChatHandler(
        Func<L4d2HostSession?> getActiveSession,
        Action<L4d2LobbyChatAudit>? audit = null)
    {
        this.getActiveSession = getActiveSession ?? throw new ArgumentNullException(nameof(getActiveSession));
        this.audit = audit ?? (static _ => { });
    }

    public override void HandleMsg(IPacketMsg packetMsg)
    {
        ArgumentNullException.ThrowIfNull(packetMsg);
        if (packetMsg.MsgType != EMsg.ClientChatMsg)
            return;

        var session = getActiveSession();
        if (session == null)
            return;

        var incoming = new ClientMsg<MsgClientChatMsg>(packetMsg);
        if (!session.TryCreateReply(
                incoming.Body.SteamIdChatRoom,
                incoming.Body.SteamIdChatter.ConvertToUInt64(),
                L4d2LobbyChatMessage.ReadPayload(incoming),
                out var reply))
        {
            return;
        }

        var senderSteamId = incoming.Body.SteamIdChatter.ConvertToUInt64();
        var lobbySteamId = incoming.Body.SteamIdChatRoom.ConvertToUInt64();
        audit(L4d2LobbyChatAudit.RequestReceived(senderSteamId, lobbySteamId, session.State));

        if (Client == null)
            return;

        Client.Send(L4d2LobbyChatMessage.CreateOutbound(
            session.LobbyId,
            new SteamID(session.OwnerSteamId),
            reply));
        audit(L4d2LobbyChatAudit.ReplySent(senderSteamId, lobbySteamId, session.State, reply.Length));
    }
}
