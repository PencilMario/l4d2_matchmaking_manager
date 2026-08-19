using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Settings;

public sealed class GlobalSettingsService(MatchmakingDbContext dbContext)
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
        settings.SteamProxyUrl = NormalizeProxyUrl(request.SteamProxyUrl);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(settings);
    }

    public async Task<string?> GetSteamProxyUrlAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CoreSettings.AsNoTracking().SingleOrDefaultAsync(
            item => item.Name == "global", cancellationToken);
        return settings?.SteamProxyUrl;
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
        new(settings.SteamProxyUrl, settings.UpdatedAt);
}
