using Microsoft.AspNetCore.Http.HttpResults;

namespace L4d2MatchmakingCore.Scheduling;

public static class WarmupStatusEndpoints
{
    public static IEndpointRouteBuilder MapWarmupStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/warmups", ListAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<WarmupStatusResponse>>> ListAsync(
        WarmupStatusService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(cancellationToken));
}
