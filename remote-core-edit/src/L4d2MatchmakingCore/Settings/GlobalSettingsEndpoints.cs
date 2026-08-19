namespace L4d2MatchmakingCore.Settings;

public static class GlobalSettingsEndpoints
{
    public static IEndpointRouteBuilder MapGlobalSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/settings").RequireAuthorization();
        group.MapGet("", GetAsync);
        group.MapPut("", UpdateAsync);
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
}
