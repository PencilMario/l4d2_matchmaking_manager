namespace L4d2MatchmakingCore.Settings;

public sealed record UpdateGlobalSettingsRequest(string? SteamProxyUrl);

public sealed record GlobalSettingsResponse(string? SteamProxyUrl, DateTimeOffset UpdatedAt);
