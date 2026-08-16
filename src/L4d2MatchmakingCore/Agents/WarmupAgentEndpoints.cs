namespace L4d2MatchmakingCore.Agents;

public static class WarmupAgentEndpoints
{
    public static RouteGroupBuilder MapWarmupAgentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapGet("/{agentId:guid}", GetAsync);
        group.MapPut("/{agentId:guid}", UpdateAsync);
        group.MapPost("/{agentId:guid}/start", StartAsync);
        group.MapPost("/{agentId:guid}/stop", StopAsync);
        group.MapPost("/{agentId:guid}/recreate", RecreateAsync);
        group.MapDelete("/{agentId:guid}", DeleteAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateWarmupAgentRequest request,
        WarmupAgentService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var agent = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/v1/agents/{agent.Id}", agent);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(exception.Message);
        }
    }

    private static async Task<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<WarmupAgentResponse>>> ListAsync(
        WarmupAgentService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(Guid agentId, WarmupAgentService service, CancellationToken cancellationToken) =>
        await service.GetAsync(agentId, cancellationToken) is { } agent
            ? Results.Ok(agent)
            : Results.NotFound();

    private static async Task<IResult> UpdateAsync(
        Guid agentId,
        UpdateWarmupAgentRequest request,
        WarmupAgentService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var agent = await service.UpdateAsync(agentId, request, cancellationToken);
            return agent is null ? Results.NotFound() : Results.Ok(agent);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(exception.Message);
        }
    }

    private static Task<IResult> StartAsync(Guid agentId, WarmupAgentService service, CancellationToken cancellationToken) =>
        MutateAsync(() => service.StartAsync(agentId, cancellationToken));

    private static Task<IResult> StopAsync(Guid agentId, WarmupAgentService service, CancellationToken cancellationToken) =>
        MutateAsync(() => service.StopAsync(agentId, cancellationToken));

    private static Task<IResult> RecreateAsync(Guid agentId, WarmupAgentService service, CancellationToken cancellationToken) =>
        MutateAsync(() => service.RecreateAsync(agentId, cancellationToken));

    private static async Task<IResult> MutateAsync(Func<Task<WarmupAgentResponse?>> mutation)
    {
        var agent = await mutation();
        return agent is null ? Results.NotFound() : Results.Ok(agent);
    }

    private static async Task<IResult> DeleteAsync(Guid agentId, WarmupAgentService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(agentId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
}
