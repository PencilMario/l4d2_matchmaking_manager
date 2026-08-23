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
        group.MapPost("/{agentId:guid}/vnc-sessions", OpenVncSessionAsync);
        group.MapPost("/{agentId:guid}/vnc-sessions/close", CloseVncSessionAsync);
        group.Map("/{agentId:guid}/vnc/{**path}", ProxyVncAsync).AllowAnonymous();
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

    private static async Task<IResult> StopAsync(
        Guid agentId,
        WarmupAgentService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return await MutateAsync(() => service.StopAsync(agentId, cancellationToken));
        }
        catch (InvalidOperationException exception) when (exception.Message == "warmup_agent_stop_drain_failed")
        {
            return Results.Conflict(exception.Message);
        }
    }

    private static async Task<IResult> RecreateAsync(
        Guid agentId,
        WarmupAgentService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return await MutateAsync(() => service.RecreateAsync(agentId, cancellationToken));
        }
        catch (InvalidOperationException exception) when (exception.Message == "warmup_agent_recreate_drain_failed")
        {
            return Results.Conflict(exception.Message);
        }
    }

    private static async Task<IResult> OpenVncSessionAsync(
        Guid agentId,
        WarmupAgentService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await service.OpenVncSessionAsync(agentId, cancellationToken);
            if (session is null)
                return Results.NotFound();
            // noVNC prefixes its `path` option with '/', so this must be relative.
            var websocketPath = $"v1/agents/{agentId}/vnc/websockify";
            var url = $"/v1/agents/{agentId}/vnc/?session={Uri.EscapeDataString(session.Token)}&path={Uri.EscapeDataString(websocketPath)}";
            return Results.Created(url, new OpenAgentVncSessionResponse(url, session.ExpiresAt));
        }
        catch (InvalidOperationException exception) when (exception.Message == "warmup_agent_not_running")
        {
            return Results.Conflict(exception.Message);
        }
        catch (InvalidOperationException)
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CloseVncSessionAsync(
        Guid agentId,
        WarmupAgentService service,
        CancellationToken cancellationToken) =>
        await service.CloseVncSessionAsync(agentId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task ProxyVncAsync(
        HttpContext context,
        Guid agentId,
        string? path,
        AgentVncSessionService sessions,
        IAgentVncProxy proxy,
        CancellationToken cancellationToken)
    {
        var initialToken = context.Request.Query["session"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(initialToken))
        {
            var exchangedSession = await sessions.ExchangeTokenAsync(agentId, initialToken, cancellationToken);
            if (exchangedSession is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            context.Response.Cookies.Append(CookieName(agentId), exchangedSession.Token, new CookieOptions
            {
                Expires = exchangedSession.ExpiresAt,
                HttpOnly = true,
                IsEssential = true,
                Path = $"/v1/agents/{agentId}/vnc",
                SameSite = SameSiteMode.Strict,
                Secure = context.Request.IsHttps,
            });
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Redirect(RemoveSessionQuery(context));
            return;
        }

        if (!context.Request.Cookies.TryGetValue(CookieName(agentId), out var cookieToken) ||
            !sessions.TryGet(agentId, cookieToken, out var session) ||
            session is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var proxyCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.LifetimeToken);
        await proxy.ProxyAsync(context, agentId, path ?? string.Empty, proxyCancellation.Token);
    }

    private static string CookieName(Guid agentId) => $"l4d2_vnc_{agentId:N}";

    private static string RemoveSessionQuery(HttpContext context)
    {
        var query = QueryString.Create(context.Request.Query
            .Where(pair => !string.Equals(pair.Key, "session", StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value))));
        var path = context.Request.Path.Value?.EndsWith("/vnc/", StringComparison.OrdinalIgnoreCase) == true
            ? $"{context.Request.Path}web/"
            : context.Request.Path.Value ?? string.Empty;
        return path + query;
    }

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
