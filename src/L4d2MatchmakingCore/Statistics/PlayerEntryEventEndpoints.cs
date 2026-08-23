using System.Security.Claims;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Auth;
using Microsoft.AspNetCore.Authorization;

namespace L4d2MatchmakingCore.Statistics;

public static class PlayerEntryEventEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEntryEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/internal/player-entry-events", IngestAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AgentReportingAuthenticationHandler.SchemeName,
            });
        return endpoints;
    }

    private static async Task<IResult> IngestAsync(
        PlayerEntryEventBatchRequest request,
        HttpContext httpContext,
        PlayerEntryEventIngestionService ingestion,
        CancellationToken cancellationToken)
    {
        var agentIdValue = httpContext.User.FindFirstValue("agent_id");
        if (!Guid.TryParse(agentIdValue, out var agentId))
            return Results.Unauthorized();

        try
        {
            var response = await ingestion.IngestAsync(agentId, request, cancellationToken);
            return Results.Accepted(value: response);
        }
        catch (PlayerEntryEventConflictException exception)
        {
            return Results.Conflict(exception.Message);
        }
        catch (PlayerEntryEventValidationException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }
}
