using System.Globalization;
using L4d2MatchmakingCore.Auth;
using Microsoft.AspNetCore.Authorization;

namespace L4d2MatchmakingCore.Statistics;

public static class PlayerEntryStatisticsEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEntryStatisticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/statistics/player-entries", QueryAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = StaticBearerAuthenticationHandler.SchemeName,
            });
        return endpoints;
    }

    private static async Task<IResult> QueryAsync(
        HttpRequest request,
        PlayerEntryStatisticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryReadDate(request.Query["from"], out var from) ||
            !TryReadDate(request.Query["to"], out var to) ||
            !TryReadGuid(request.Query["targetServerId"], out var targetServerId) ||
            !TryReadGuid(request.Query["agentId"], out var agentId))
            return Results.BadRequest("player_entry_statistics_query_invalid");

        try
        {
            var result = await query.QueryAsync(new PlayerEntryStatisticsQueryOptions(
                from,
                to,
                request.Query["granularity"].ToString(),
                request.Query["lobbyType"].ToString(),
                request.Query["targetMode"].ToString(),
                targetServerId,
                agentId), cancellationToken);
            return Results.Ok(result);
        }
        catch (PlayerEntryStatisticsQueryValidationException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private static bool TryReadDate(string? value, out DateTimeOffset? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            result = parsed;
            return true;
        }
        result = null;
        return false;
    }

    private static bool TryReadGuid(string? value, out Guid? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }
        if (Guid.TryParse(value, out var parsed))
        {
            result = parsed;
            return true;
        }
        result = null;
        return false;
    }
}
