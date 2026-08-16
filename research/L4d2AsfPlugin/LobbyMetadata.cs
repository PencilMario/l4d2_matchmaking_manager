namespace L4d2AsfPlugin;

public static class LobbyMetadata
{
    public static IReadOnlyDictionary<string, string> CreateSessionMetadata() =>
        RealSessionSettings.CreateLobbyMetadata();
}
