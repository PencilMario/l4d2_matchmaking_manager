using L4d2MatchmakingCore.Agents;

namespace L4d2MatchmakingCore.Lobbies;

public static class LobbyQueryEndpoints
{
    public static IEndpointRouteBuilder MapLobbyQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/lobbies/{lobbyId}", QueryAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> QueryAsync(
        string lobbyId,
        IHealthyAgentSelector selector,
        IAgentControlClient agents,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(lobbyId, out var parsedLobbyId) || parsedLobbyId == 0)
            return Results.BadRequest("invalid_lobby_id");
        var agent = await selector.SelectAsync(cancellationToken);
        if (agent is null)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        try
        {
            return Results.Ok(await agents.ReadLobbyAsync(agent, lobbyId, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
