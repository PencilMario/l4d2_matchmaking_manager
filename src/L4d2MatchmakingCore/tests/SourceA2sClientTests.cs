using System.Net;
using System.Net.Sockets;
using L4d2MatchmakingCore.A2s;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class SourceA2sClientTests
{
    [TestMethod]
    public async Task GetInfoRepliesToChallengeAndParsesServerMetadata()
    {
        await using var server = await FakeA2sServer.StartAsync(1234, 4);
        var client = new SourceA2sClient(TimeSpan.FromSeconds(2));

        var info = await client.GetInfoAsync(server.Endpoint, CancellationToken.None);

        Assert.AreEqual("name", info.ServerName);
        Assert.AreEqual(4, info.PlayerCount);
        Assert.AreEqual(8, info.MaxPlayers);
        Assert.AreEqual(500U, info.AppId);
        Assert.IsTrue(info.ObservedAt > DateTimeOffset.UtcNow.AddSeconds(-2));
        Assert.AreEqual(2, server.RequestCount);
    }

    [TestMethod]
    public async Task GetInfoRejectsResponseTruncatedBeforeMaxPlayers()
    {
        await using var server = await FakeA2sServer.StartAsync(1234, 4, truncateAfterPlayers: true);
        var client = new SourceA2sClient(TimeSpan.FromSeconds(2));

        var exception = await Assert.ThrowsExceptionAsync<InvalidDataException>(async () =>
            await client.GetInfoAsync(server.Endpoint, CancellationToken.None));

        Assert.AreEqual("a2s_truncated_info_response", exception.Message);
    }

    private sealed class FakeA2sServer : IAsyncDisposable
    {
        private readonly UdpClient _socket;
        private Task _completion = Task.CompletedTask;

        private FakeA2sServer(UdpClient socket) => _socket = socket;

        public IPEndPoint Endpoint => (IPEndPoint)_socket.Client.LocalEndPoint!;
        public int RequestCount { get; private set; }

        public static async Task<FakeA2sServer> StartAsync(int challenge, byte players, bool truncateAfterPlayers = false)
        {
            var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var server = new FakeA2sServer(socket);
            server._completion = server.RunAsync(challenge, players, truncateAfterPlayers);
            await Task.Yield();
            return server;
        }

        private async Task RunAsync(int challenge, byte players, bool truncateAfterPlayers)
        {
            var first = await _socket.ReceiveAsync();
            RequestCount++;
            await _socket.SendAsync(ChallengeResponse(challenge), first.RemoteEndPoint);
            var second = await _socket.ReceiveAsync();
            RequestCount++;
            await _socket.SendAsync(InfoResponse(players, truncateAfterPlayers), second.RemoteEndPoint);
        }

        public async ValueTask DisposeAsync()
        {
            _socket.Dispose();
            await _completion;
        }

        private static byte[] ChallengeResponse(int challenge) => [255, 255, 255, 255, 0x41, .. BitConverter.GetBytes(challenge)];

        private static byte[] InfoResponse(byte players, bool truncateAfterPlayers) =>
            truncateAfterPlayers
                ? [255, 255, 255, 255, 0x49, 17, .. System.Text.Encoding.UTF8.GetBytes("name\0map\0folder\0game\0"), 244, 1, players]
                : [255, 255, 255, 255, 0x49, 17, .. System.Text.Encoding.UTF8.GetBytes("name\0map\0folder\0game\0"), 244, 1, players, 8, 0, (byte)'d', (byte)'l', 0, 1, .. System.Text.Encoding.UTF8.GetBytes("1.0.4.7\0"), 0];
    }
}
