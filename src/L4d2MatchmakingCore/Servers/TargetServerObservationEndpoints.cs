using Microsoft.AspNetCore.Http.HttpResults;

namespace L4d2MatchmakingCore.Servers;

public static class TargetServerObservationEndpoints
{
    public static RouteGroupBuilder MapTargetServerObservationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/observations", ListAsync);
        return group;
    }

    private static async Task<Ok<IReadOnlyList<TargetServerObservationResponse>>> ListAsync(
        TargetServerObservationService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(cancellationToken));
}
