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
        LobbyQueryService queries,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(lobbyId, out var parsedLobbyId) || parsedLobbyId == 0)
            return Results.BadRequest("invalid_lobby_id");
        try
        {
            return Results.Ok(await queries.QueryAsync(lobbyId, cancellationToken));
        }
        catch (AgentLobbyQueryException exception) when (exception.Code == "lobby_data_unavailable")
        {
            return Results.Json("lobby_data_unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (AgentLobbyQueryException exception) when (exception.Code == "lobby_operation_preservation_failed")
        {
            return Results.Json("lobby_operation_preservation_failed", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException)
        {
            return Results.Json("lobby_query_agent_unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
