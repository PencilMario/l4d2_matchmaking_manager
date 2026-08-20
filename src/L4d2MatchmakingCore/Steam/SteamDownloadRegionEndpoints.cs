namespace L4d2MatchmakingCore.Steam;

public static class SteamDownloadRegionEndpoints
{
    public static IEndpointRouteBuilder MapSteamDownloadRegionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/steam/download-regions", (SteamDownloadRegionCatalog catalog) =>
            Results.Ok(catalog.List())).RequireAuthorization();
        return endpoints;
    }
}
