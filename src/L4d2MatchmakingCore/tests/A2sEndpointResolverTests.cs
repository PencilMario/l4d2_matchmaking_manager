using System.Net.Sockets;
using L4d2MatchmakingCore.A2s;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class A2sEndpointResolverTests
{
    [TestMethod]
    public async Task ResolveIpv4AsyncReturnsIpv4EndpointForLocalhost()
    {
        var endpoint = await A2sEndpointResolver.ResolveIpv4Async("localhost", 27015, CancellationToken.None);

        Assert.IsNotNull(endpoint);
        Assert.AreEqual(AddressFamily.InterNetwork, endpoint.AddressFamily);
        Assert.AreEqual(27015, endpoint.Port);
    }

    [TestMethod]
    public async Task ResolveIpv4AsyncReturnsNullForInvalidHost()
    {
        var endpoint = await A2sEndpointResolver.ResolveIpv4Async("invalid host", 27015, CancellationToken.None);

        Assert.IsNull(endpoint);
    }

    [TestMethod]
    public async Task ResolveIpv4AsyncPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await A2sEndpointResolver.ResolveIpv4Async("localhost", 27015, cancellation.Token));
    }
}
