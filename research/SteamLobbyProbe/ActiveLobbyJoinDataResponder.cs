internal sealed class ActiveLobbyJoinDataResponder
{
    private readonly ulong _ownerSteamId;
    private readonly string _connectString;

    internal ActiveLobbyJoinDataResponder(ulong lobbyId, ulong ownerSteamId, string connectString)
    {
        LobbyId = lobbyId;
        _ownerSteamId = ownerSteamId;
        _connectString = connectString;
    }

    internal ulong LobbyId { get; }

    internal bool TryCreateReply(
        ulong chatLobbyId,
        ReadOnlySpan<byte> request,
        ulong senderSteamId,
        out byte[] reply)
    {
        reply = Array.Empty<byte>();
        return chatLobbyId == LobbyId && senderSteamId != 0 &&
            LobbyJoinProtocol.TryCreateRealReply(
                request,
                _connectString,
                LobbyId,
                _ownerSteamId,
                senderSteamId,
                out reply);
    }
}
