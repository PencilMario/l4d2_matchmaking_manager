namespace L4d2MatchmakingCore.Settings;

public sealed record UpdateGlobalSettingsRequest(
    string? SteamProxyUrl = null,
    string? SteamWebApiKey = null,
    bool ClearSteamWebApiKey = false);

public sealed record GlobalSettingsResponse(
    string? SteamProxyUrl,
    bool SteamWebApiKeyConfigured,
    DateTimeOffset UpdatedAt);
