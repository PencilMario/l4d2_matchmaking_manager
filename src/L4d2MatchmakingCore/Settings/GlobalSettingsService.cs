using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Servers;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Settings;

public interface ISteamWebApiKeyProvider
{
    Task<string?> GetSteamWebApiKeyAsync(CancellationToken cancellationToken);
}

public sealed class GlobalSettingsService(
    MatchmakingDbContext dbContext,
    ISecretProtector secretProtector) : ISteamWebApiKeyProvider
{
    public async Task<GlobalSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return ToResponse(settings);
    }

    public async Task<GlobalSettingsResponse> UpdateAsync(
        UpdateGlobalSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        if (request.ClearSteamWebApiKey && !string.IsNullOrWhiteSpace(request.SteamWebApiKey))
            throw new ArgumentException("steam_web_api_key_update_conflict");
        settings.SteamProxyUrl = NormalizeProxyUrl(request.SteamProxyUrl);
        if (request.ClearSteamWebApiKey)
            settings.SteamWebApiKeyCiphertext = null;
        else if (!string.IsNullOrWhiteSpace(request.SteamWebApiKey))
            settings.SteamWebApiKeyCiphertext = secretProtector.Protect(request.SteamWebApiKey.Trim());
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(settings);
    }

    public async Task<VncProxySettingsResponse> GetVncProxyAsync(CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return new VncProxySettingsResponse(settings.SteamProxyUrl, settings.UpdatedAt);
    }

    public async Task<VncProxySettingsResponse> UpdateVncProxyAsync(
        UpdateVncProxyRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        settings.SteamProxyUrl = NormalizeProxyUrl(request.ProxyUrl);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new VncProxySettingsResponse(settings.SteamProxyUrl, settings.UpdatedAt);
    }

    public async Task<SteamWebApiKeySettingsResponse> GetSteamWebApiKeySettingsAsync(
        CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return new SteamWebApiKeySettingsResponse(
            settings.SteamWebApiKeyCiphertext is not null,
            settings.UpdatedAt);
    }

    public async Task<SteamWebApiKeySettingsResponse> UpdateSteamWebApiKeyAsync(
        UpdateSteamWebApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Clear && !string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException("steam_web_api_key_update_conflict");

        var settings = await GetOrCreateAsync(cancellationToken);
        if (request.Clear)
            settings.SteamWebApiKeyCiphertext = null;
        else if (!string.IsNullOrWhiteSpace(request.ApiKey))
            settings.SteamWebApiKeyCiphertext = secretProtector.Protect(request.ApiKey.Trim());

        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SteamWebApiKeySettingsResponse(
            settings.SteamWebApiKeyCiphertext is not null,
            settings.UpdatedAt);
    }

    public async Task<string?> GetSteamProxyUrlAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CoreSettings.AsNoTracking().SingleOrDefaultAsync(
            item => item.Name == "global", cancellationToken);
        return settings?.SteamProxyUrl;
    }

    public async Task<string?> GetSteamWebApiKeyAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CoreSettings.AsNoTracking().SingleOrDefaultAsync(
            item => item.Name == "global", cancellationToken);
        return secretProtector.Unprotect(settings?.SteamWebApiKeyCiphertext);
    }

    public static string? NormalizeProxyUrl(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length > 0 ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("invalid_steam_proxy_url");
        return uri.ToString();
    }

    private async Task<CoreSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CoreSettings.SingleOrDefaultAsync(
            item => item.Name == "global", cancellationToken);
        if (settings is not null)
            return settings;
        settings = new CoreSettings { UpdatedAt = DateTimeOffset.UtcNow };
        dbContext.CoreSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static GlobalSettingsResponse ToResponse(CoreSettings settings) =>
        new(settings.SteamProxyUrl, settings.SteamWebApiKeyCiphertext is not null, settings.UpdatedAt);
}
