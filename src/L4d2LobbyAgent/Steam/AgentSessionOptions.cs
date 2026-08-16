public sealed record AgentSessionOptions(string SteamApiLibraryPath)
{
    public static AgentSessionOptions FromEnvironment() =>
        new(Environment.GetEnvironmentVariable("STEAM_API_LIBRARY_PATH") ?? string.Empty);
}
