using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using L4d2MatchmakingCore.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace L4d2MatchmakingCore.Auth;

public sealed class AgentReportingAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentReporting";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter) ||
            !AgentReportingTokenService.TryParse(header.Parameter, out var agentId))
            return AuthenticateResult.NoResult();

        await using var scope = scopeFactory.CreateAsyncScope();
        var agent = await scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>()
            .WarmupAgents.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == agentId, Context.RequestAborted);
        if (agent is null || !AgentReportingTokenService.Matches(header.Parameter, agent))
            return AuthenticateResult.Fail("invalid_agent_reporting_token");

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, agentId.ToString()), new Claim("agent_id", agentId.ToString())],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
