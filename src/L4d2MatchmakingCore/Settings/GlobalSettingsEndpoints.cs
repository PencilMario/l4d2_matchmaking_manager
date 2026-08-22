namespace L4d2MatchmakingCore.Settings;

public static class GlobalSettingsEndpoints
{
    public static IEndpointRouteBuilder MapGlobalSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/settings").RequireAuthorization();
        group.MapGet("", GetAsync);
        group.MapPut("", UpdateAsync);
        group.MapGet("/vnc-proxy", GetVncProxyAsync);
        group.MapPut("/vnc-proxy", UpdateVncProxyAsync);
        group.MapGet("/steam-web-api-key", GetSteamWebApiKeyAsync);
        group.MapPut("/steam-web-api-key", UpdateSteamWebApiKeyAsync);
        group.MapGet("/warmup-scheduling", GetWarmupSchedulingAsync);
        group.MapPut("/warmup-scheduling", UpdateWarmupSchedulingAsync);
        group.MapGet("/warmup-pause-windows", GetWarmupPauseWindowsAsync);
        group.MapPut("/warmup-pause-windows", UpdateWarmupPauseWindowsAsync);
        return endpoints;
    }

    private static Task<GlobalSettingsResponse> GetAsync(
        GlobalSettingsService service,
        CancellationToken cancellationToken) => service.GetAsync(cancellationToken);

    private static async Task<IResult> UpdateAsync(
        UpdateGlobalSettingsRequest request,
        GlobalSettingsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private static Task<VncProxySettingsResponse> GetVncProxyAsync(
        GlobalSettingsService service,
        CancellationToken cancellationToken) => service.GetVncProxyAsync(cancellationToken);

    private static async Task<IResult> UpdateVncProxyAsync(
        UpdateVncProxyRequest request,
        GlobalSettingsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateVncProxyAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private static Task<SteamWebApiKeySettingsResponse> GetSteamWebApiKeyAsync(
        GlobalSettingsService service,
        CancellationToken cancellationToken) => service.GetSteamWebApiKeySettingsAsync(cancellationToken);

    private static async Task<IResult> UpdateSteamWebApiKeyAsync(
        UpdateSteamWebApiKeyRequest request,
        GlobalSettingsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateSteamWebApiKeyAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private static Task<WarmupSchedulingSettingsResponse> GetWarmupSchedulingAsync(
        GlobalSettingsService service,
        CancellationToken cancellationToken) => service.GetWarmupSchedulingAsync(cancellationToken);

    private static async Task<IResult> UpdateWarmupSchedulingAsync(
        UpdateWarmupSchedulingRequest request,
        GlobalSettingsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateWarmupSchedulingAsync(request, cancellationToken));
        }
        catch (InvalidOperationException exception) when (
            exception.Message is "global_warmup_drain_failed" or "global_warmup_drain_pending")
        {
            return Results.Conflict(exception.Message);
        }
    }

    private static Task<WarmupPauseWindowsSettingsResponse> GetWarmupPauseWindowsAsync(
        GlobalSettingsService service,
        CancellationToken cancellationToken) => service.GetWarmupPauseWindowsAsync(cancellationToken);

    private static async Task<IResult> UpdateWarmupPauseWindowsAsync(
        UpdateWarmupPauseWindowsRequest request,
        GlobalSettingsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateWarmupPauseWindowsAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception) when (exception.Message == "global_warmup_drain_failed")
        {
            return Results.Conflict(exception.Message);
        }
    }
}
