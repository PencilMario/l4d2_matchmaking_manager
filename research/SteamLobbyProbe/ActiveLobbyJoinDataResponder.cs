internal sealed class ActiveLobbyJoinDataResponder
{
    private readonly ulong _ownerSteamId;
    private readonly string _connectString;
    private readonly string? _gameMode;

    internal ActiveLobbyJoinDataResponder(ulong lobbyId, ulong ownerSteamId, string connectString, string? gameMode = null)
    {
        LobbyId = lobbyId;
        _ownerSteamId = ownerSteamId;
        _connectString = connectString;
        _gameMode = gameMode;
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
                _gameMode,
                out reply);
    }
}
