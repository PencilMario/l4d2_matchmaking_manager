using SteamKit2;
using SteamKit2.Internal;

namespace L4d2AsfPlugin;

internal static class L4d2LobbyChatMessage
{
    internal static ClientMsg<MsgClientChatMsg> CreateOutbound(
        SteamID lobbyId,
        SteamID senderId,
        ReadOnlySpan<byte> payload)
    {
        var message = new ClientMsg<MsgClientChatMsg>(payload.Length);
        message.Body.ChatMsgType = EChatEntryType.ChatMsg;
        message.Body.SteamIdChatRoom = lobbyId;
        message.Body.SteamIdChatter = senderId;
        message.Payload.Write(payload);
        return message;
    }

    internal static byte[] ReadPayload(ClientMsg<MsgClientChatMsg> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.Payload.ToArray();
    }
}
