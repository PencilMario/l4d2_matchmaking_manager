namespace L4d2MatchmakingCore.A2s;

public sealed record A2sServerInfo(
    string ServerName,
    int PlayerCount,
    int MaxPlayers,
    DateTimeOffset ObservedAt);
