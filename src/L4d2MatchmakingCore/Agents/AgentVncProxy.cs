using System.Net.WebSockets;
using System.Net.Http.Headers;

namespace L4d2MatchmakingCore.Agents;

public interface IAgentVncProxy
{
    Task ProxyAsync(HttpContext context, Guid agentId, string path, CancellationToken cancellationToken);
}

public interface IAgentVncProxyTargetResolver
{
    Uri Resolve(Guid agentId, string path, string scheme, QueryString query);
}

public sealed class AgentVncProxyTargetResolver : IAgentVncProxyTargetResolver
{
    public Uri Resolve(Guid agentId, string path, string scheme, QueryString query) =>
        new UriBuilder(scheme, $"l4d2-agent-{agentId:N}", 8083, "/" + path.TrimStart('/'))
        {
            Query = query.Value?.TrimStart('?'),
        }.Uri;
}

public sealed class AgentVncProxy(
    IHttpClientFactory httpClientFactory,
    IAgentVncProxyTargetResolver targetResolver) : IAgentVncProxy
{
    private static readonly HashSet<string> RequestHeadersToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Connection", "Cookie", "Host", "Referer", "Upgrade",
    };

    private static readonly HashSet<string> ResponseHeadersToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
        "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Set-Cookie",
    };

    public Task ProxyAsync(HttpContext context, Guid agentId, string path, CancellationToken cancellationToken) =>
        context.WebSockets.IsWebSocketRequest
            ? ProxyWebSocketAsync(context, agentId, path, cancellationToken)
            : ProxyHttpAsync(context, agentId, path, cancellationToken);

    private async Task ProxyHttpAsync(HttpContext context, Guid agentId, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), BuildUri(context, agentId, path, "http"));
        CopyRequestHeaders(context.Request.Headers, request.Headers, RequestHeadersToSkip);
        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            CopyRequestHeaders(context.Request.Headers, request.Content.Headers, RequestHeadersToSkip);
        }

        using var response = await httpClientFactory.CreateClient("agent-vnc").SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        context.Response.StatusCode = (int)response.StatusCode;
        CopyResponseHeaders(response.Headers, context.Response.Headers, ResponseHeadersToSkip, context, agentId);
        CopyResponseHeaders(response.Content.Headers, context.Response.Headers, ResponseHeadersToSkip, context, agentId);
        await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
    }

    private async Task ProxyWebSocketAsync(HttpContext context, Guid agentId, string path, CancellationToken cancellationToken)
    {
        using var upstream = new ClientWebSocket();
        foreach (var protocol in context.Request.Headers.SecWebSocketProtocol.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            upstream.Options.AddSubProtocol(protocol);
        }
        await upstream.ConnectAsync(BuildUri(context, agentId, path, "ws"), cancellationToken);
        using var downstream = await context.WebSockets.AcceptWebSocketAsync(upstream.SubProtocol);
        using var relayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var logger = context.RequestServices.GetRequiredService<ILogger<AgentVncProxy>>();
        var inbound = RelayAsync(downstream, upstream, "browser_to_agent", logger, relayCancellation.Token);
        var outbound = RelayAsync(upstream, downstream, "agent_to_browser", logger, relayCancellation.Token);
        await Task.WhenAny(inbound, outbound);
        relayCancellation.Cancel();
        await ObserveAsync(inbound);
        await ObserveAsync(outbound);
    }

    private static async Task RelayAsync(
        WebSocket source,
        WebSocket destination,
        string direction,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var received = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogWarning(
                        "VNC websocket relay closed in {Direction}: {CloseStatus} {CloseDescription}",
                        direction,
                        received.CloseStatus,
                        received.CloseStatusDescription);
                    if (destination.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        await destination.CloseOutputAsync(received.CloseStatus ?? WebSocketCloseStatus.NormalClosure, received.CloseStatusDescription, CancellationToken.None);
                    return;
                }
                await destination.SendAsync(new ArraySegment<byte>(buffer, 0, received.Count), received.MessageType, received.EndOfMessage, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            logger.LogWarning(exception, "VNC websocket relay failed in {Direction}", direction);
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Uri BuildUri(HttpContext context, Guid agentId, string path, string scheme)
    {
        var query = QueryString.Create(context.Request.Query
            .Where(pair => !string.Equals(pair.Key, "session", StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value))));
        return targetResolver.Resolve(agentId, path, scheme, query);
    }

    private static void CopyRequestHeaders(
        Microsoft.AspNetCore.Http.IHeaderDictionary source,
        HttpHeaders destination,
        ISet<string> skipped)
    {
        foreach (var (name, value) in source)
        {
            if (!skipped.Contains(name))
                destination.TryAddWithoutValidation(name, value.AsEnumerable());
        }
    }

    private static void CopyResponseHeaders(
        HttpHeaders source,
        Microsoft.AspNetCore.Http.IHeaderDictionary destination,
        ISet<string> skipped,
        HttpContext context,
        Guid agentId)
    {
        foreach (var (name, value) in source)
        {
            if (skipped.Contains(name))
                continue;
            destination[name] = name.Equals("Location", StringComparison.OrdinalIgnoreCase)
                ? RewriteLocation(value, context, agentId)
                : new Microsoft.Extensions.Primitives.StringValues(value.ToArray());
        }
    }

    private static string RewriteLocation(
        IEnumerable<string> location,
        HttpContext context,
        Guid agentId)
    {
        var value = location.FirstOrDefault() ?? string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            value = absolute.PathAndQuery;
        if (!value.StartsWith("/", StringComparison.Ordinal))
            return value;
        return $"/v1/agents/{agentId}/vnc{value}";
    }
}
