using System.Text.Json;

namespace L4d2MatchmakingCore.Steam;

public sealed record SteamDownloadRegionResponse(int Id, string Name);

public sealed class SteamDownloadRegionCatalog
{
    private const string ResourceName = "L4d2MatchmakingCore.Steam.steam-download-regions.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<SteamDownloadRegionResponse> regions;

    public SteamDownloadRegionCatalog()
    {
        using var stream = typeof(SteamDownloadRegionCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("steam_download_region_catalog_missing");
        regions = JsonSerializer.Deserialize<List<SteamDownloadRegionResponse>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("steam_download_region_catalog_invalid");
        if (regions.Count == 0 || regions.Any(region => region.Id < 0 || string.IsNullOrWhiteSpace(region.Name)))
            throw new InvalidOperationException("steam_download_region_catalog_invalid");
    }

    public IReadOnlyList<SteamDownloadRegionResponse> List() => regions;
}
