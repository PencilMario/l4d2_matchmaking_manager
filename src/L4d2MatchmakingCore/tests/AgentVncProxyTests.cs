using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using L4d2MatchmakingCore.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class AgentVncProxyTests
{
    [TestMethod]
    public async Task HttpProxyRemovesSessionAndRefererBeforeForwarding()
    {
        var handler = new CapturingHandler();
        var proxy = new AgentVncProxy(
            new TestHttpClientFactory(new HttpClient(handler)),
            new AgentVncProxyTargetResolver());
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString("?session=one-time&autoconnect=true");
        context.Request.Headers.Referer = "http://manager/v1/agents/id/vnc/?session=one-time";
        context.Response.Body = new MemoryStream();

        await proxy.ProxyAsync(context, Guid.Parse("11111111-1111-1111-1111-111111111111"), "vnc.html", CancellationToken.None);

        Assert.IsNotNull(handler.Request);
        Assert.AreEqual("http://l4d2-agent-11111111111111111111111111111111:8083/vnc.html?autoconnect=true", handler.Request.RequestUri?.ToString());
        Assert.IsFalse(handler.Request.Headers.Contains("Referer"));
        Assert.AreEqual("/v1/agents/11111111-1111-1111-1111-111111111111/vnc/login", context.Response.Headers.Location);
    }

    [TestMethod]
    public async Task WebSocketProxyStopsAnEstablishedTunnelWhenSessionIsCancelled()
    {
        var upstreamConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var upstream = await TemporaryWebSocketServer.StartAsync(async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            upstreamConnected.TrySetResult();
            try
            {
                await socket.ReceiveAsync(new ArraySegment<byte>(new byte[32]), CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        });
        using var sessionLifetime = new CancellationTokenSource();
        var proxy = new AgentVncProxy(
            new TestHttpClientFactory(new HttpClient(new CapturingHandler())),
            new LoopbackTargetResolver(upstream.WebSocketUri));
        await using var gateway = await TemporaryWebSocketServer.StartAsync(
            context => proxy.ProxyAsync(
                context,
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "websockify",
                sessionLifetime.Token));
        using var browser = new ClientWebSocket();

        await browser.ConnectAsync(gateway.WebSocketUri, CancellationToken.None);
        await upstreamConnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        sessionLifetime.Cancel();

        var receive = browser.ReceiveAsync(new ArraySegment<byte>(new byte[32]), CancellationToken.None);
        var completed = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(receive, completed);
        try
        {
            Assert.AreEqual(WebSocketMessageType.Close, (await receive).MessageType);
        }
        catch (WebSocketException)
        {
            // Closing the proxy-owned downstream socket may abort instead of completing the close handshake.
        }
        Assert.AreNotEqual(WebSocketState.Open, browser.State);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok"),
            };
            response.Headers.Location = new Uri("/login", UriKind.Relative);
            return Task.FromResult(response);
        }
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class LoopbackTargetResolver(Uri upstream) : IAgentVncProxyTargetResolver
    {
        public Uri Resolve(Guid agentId, string path, string scheme, QueryString query) =>
            new UriBuilder(scheme, upstream.Host, upstream.Port, "/" + path.TrimStart('/'))
            {
                Query = query.Value?.TrimStart('?'),
            }.Uri;
    }

    private sealed class TemporaryWebSocketServer(WebApplication application, Uri webSocketUri) : IAsyncDisposable
    {
        public Uri WebSocketUri { get; } = webSocketUri;

        public static async Task<TemporaryWebSocketServer> StartAsync(Func<HttpContext, Task> handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            var application = builder.Build();
            application.UseWebSockets();
            application.Map("/{**path}", handler);
            await application.StartAsync();
            var addresses = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>();
            var address = addresses?.Addresses.SingleOrDefault();
            if (address is null)
            {
                await application.DisposeAsync();
                throw new InvalidOperationException("websocket_test_server_address_unavailable");
            }
            var uri = new Uri(address);
            return new TemporaryWebSocketServer(application, new UriBuilder("ws", uri.Host, uri.Port).Uri);
        }

        public ValueTask DisposeAsync() => application.DisposeAsync();
    }
}
