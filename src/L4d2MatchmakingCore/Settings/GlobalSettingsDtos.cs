using L4d2MatchmakingCore.Scheduling;

namespace L4d2MatchmakingCore.Settings;

public sealed record UpdateGlobalSettingsRequest(
    string? SteamProxyUrl = null,
    string? SteamWebApiKey = null,
    bool ClearSteamWebApiKey = false);

public sealed record GlobalSettingsResponse(
    string? SteamProxyUrl,
    bool SteamWebApiKeyConfigured,
    bool WarmupSchedulingEnabled,
    IReadOnlyList<WarmupPauseWindow> WarmupPauseWindows,
    bool WarmupPauseWindowsActive,
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

public sealed record UpdateWarmupPauseWindowsRequest(
    IReadOnlyList<WarmupPauseWindow>? Windows = null);

public sealed record WarmupPauseWindowsSettingsResponse(
    IReadOnlyList<WarmupPauseWindow> Windows,
    bool Active,
    DateTimeOffset UpdatedAt);
