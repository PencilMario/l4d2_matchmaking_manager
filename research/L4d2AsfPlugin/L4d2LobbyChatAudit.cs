namespace L4d2AsfPlugin;

internal enum L4d2LobbyChatAuditEvent
{
    RequestJoinData,
    ReplyJoinData
}

internal readonly record struct L4d2LobbyChatAudit(
    L4d2LobbyChatAuditEvent Event,
    ulong SenderSteamId,
    ulong LobbySteamId,
    L4d2HostSessionState State,
    int ReplyLength)
{
    internal static L4d2LobbyChatAudit RequestReceived(
        ulong senderSteamId,
        ulong lobbySteamId,
        L4d2HostSessionState state) => new(
            L4d2LobbyChatAuditEvent.RequestJoinData,
            senderSteamId,
            lobbySteamId,
            state,
            ReplyLength: 0);

    internal static L4d2LobbyChatAudit ReplySent(
        ulong senderSteamId,
        ulong lobbySteamId,
        L4d2HostSessionState state,
        int replyLength) => new(
            L4d2LobbyChatAuditEvent.ReplyJoinData,
            senderSteamId,
            lobbySteamId,
            state,
            replyLength);
}
