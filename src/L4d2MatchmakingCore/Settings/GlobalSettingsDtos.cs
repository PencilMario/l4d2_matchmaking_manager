namespace L4d2MatchmakingCore.Settings;

public sealed record UpdateGlobalSettingsRequest(
    string? SteamProxyUrl = null,
    string? SteamWebApiKey = null,
    bool ClearSteamWebApiKey = false);

public sealed record GlobalSettingsResponse(
    string? SteamProxyUrl,
    bool SteamWebApiKeyConfigured,
    bool WarmupSchedulingEnabled,
    DateTimeOffset UpdatedAt);

public sealed record UpdateVncProxyRequest(string? ProxyUrl = null);

public sealed record VncProxySettingsResponse(
    string? ProxyUrl,
    DateTimeOffset UpdatedAt);

public sealed record UpdateSteamWebApiKeyRequest(
    string? ApiKey = null,
    bool Clear = false);

public sealed record SteamWebApiKeySettingsResponse(
    bool Configured,
    DateTimeOffset UpdatedAt);

public sealed record UpdateWarmupSchedulingRequest(bool Enabled);

public sealed record WarmupSchedulingSettingsResponse(
    bool Enabled,
    DateTimeOffset UpdatedAt);
