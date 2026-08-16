using Microsoft.AspNetCore.Http.HttpResults;

namespace L4d2MatchmakingCore.Servers;

public static class TargetServerEndpoints
{
    public static RouteGroupBuilder MapTargetServerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapGet("/{serverId:guid}", GetAsync);
        group.MapPut("/{serverId:guid}", UpdateAsync);
        group.MapDelete("/{serverId:guid}", DeleteAsync);
        return group;
    }

    private static async Task<Results<Created<TargetServerResponse>, BadRequest<string>>> CreateAsync(
        CreateTargetServerRequest request,
        TargetServerService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var server = await service.CreateAsync(request, cancellationToken);
            return TypedResults.Created($"/v1/servers/{server.Id}", server);
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }
    }

    private static async Task<Ok<IReadOnlyList<TargetServerResponse>>> ListAsync(
        TargetServerService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(cancellationToken));

    private static async Task<Results<Ok<TargetServerResponse>, NotFound>> GetAsync(
        Guid serverId,
        TargetServerService service,
        CancellationToken cancellationToken)
    {
        var server = await service.GetAsync(serverId, cancellationToken);
        return server is null ? TypedResults.NotFound() : TypedResults.Ok(server);
    }

    private static async Task<Results<Ok<TargetServerResponse>, NotFound, BadRequest<string>>> UpdateAsync(
        Guid serverId,
        UpdateTargetServerRequest request,
        TargetServerService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var server = await service.UpdateAsync(serverId, request, cancellationToken);
            return server is null ? TypedResults.NotFound() : TypedResults.Ok(server);
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid serverId,
        TargetServerService service,
        CancellationToken cancellationToken) =>
        await service.DeleteAsync(serverId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
