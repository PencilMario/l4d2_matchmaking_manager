using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2Protocol.Tests;

[TestClass]
public sealed class SourceRconClientTests
{
    private const ulong Cookie = 0x0186000059AD6053;

    [TestMethod]
    public async Task VerifyReservationAcceptsOnlyTheCurrentLobbyCookie()
    {
        var verifier = GetVerifier();
        using var matching = new SourceRconFixture($"players : 0 humans\nreserved {Cookie:x}\n");
        var matchingEndpoint = matching.Endpoint;
        var matchingTask = matching.ServeAsync("rcon-password");

        var verified = verifier(matchingEndpoint, "rcon-password", Cookie, TimeSpan.FromSeconds(1));

        await matchingTask;
        Assert.IsTrue(verified);

        using var differentCookie = new SourceRconFixture("players : 0 humans\nreserved 186000059ad6054\n");
        var differentCookieEndpoint = differentCookie.Endpoint;
        var differentCookieTask = differentCookie.ServeAsync("rcon-password");

        var rejected = verifier(differentCookieEndpoint, "rcon-password", Cookie, TimeSpan.FromSeconds(1));

        await differentCookieTask;
        Assert.IsFalse(rejected);
    }

    private static Func<IPEndPoint, string, ulong, TimeSpan, bool> GetVerifier()
    {
        var type = typeof(ReservationProtocol).Assembly.GetType("SourceRconClient");
        Assert.IsNotNull(type, "SourceRconClient must own reservation status verification.");
        var method = type.GetMethod("VerifyReservation", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "SourceRconClient must expose VerifyReservation.");

        return (endpoint, password, cookie, timeout) =>
            (bool)(method.Invoke(null, [endpoint, password, cookie, timeout])
                ?? throw new AssertFailedException("SourceRconClient must return a verification result."));
    }

    private sealed class SourceRconFixture(string status) : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        internal IPEndPoint Endpoint
        {
            get
            {
                _listener.Start();
                return (IPEndPoint)_listener.LocalEndpoint;
            }
        }

        internal async Task ServeAsync(string expectedPassword)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var authentication = await ReadPacketAsync(stream);
            Assert.AreEqual(3, authentication.Type);
            Assert.AreEqual(expectedPassword, authentication.Body);
            await WritePacketAsync(stream, authentication.Id, 2, string.Empty);

            var command = await ReadPacketAsync(stream);
            Assert.AreEqual(2, command.Type);
            Assert.AreEqual("status", command.Body);
            await WritePacketAsync(stream, command.Id, 0, status);
        }

        public void Dispose() => _listener.Stop();

        private static async Task<RconPacket> ReadPacketAsync(NetworkStream stream)
        {
            var lengthBytes = await ReadExactlyAsync(stream, sizeof(int));
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            var payload = await ReadExactlyAsync(stream, length);
            return new RconPacket(
                BinaryPrimitives.ReadInt32LittleEndian(payload),
                BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int))),
                Encoding.UTF8.GetString(payload.AsSpan(sizeof(int) * 2, payload.Length - (sizeof(int) * 2 + 2))));
        }

        private static async Task WritePacketAsync(NetworkStream stream, int id, int type, string body)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var payload = new byte[sizeof(int) * 2 + bodyBytes.Length + 2];
            BinaryPrimitives.WriteInt32LittleEndian(payload, id);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), type);
            bodyBytes.CopyTo(payload.AsSpan(sizeof(int) * 2));
            var length = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
            await stream.WriteAsync(length);
            await stream.WriteAsync(payload);
        }

        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length)
        {
            var buffer = new byte[length];
            for (var offset = 0; offset < buffer.Length;)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset));
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }

        private sealed record RconPacket(int Id, int Type, string Body);
    }
}
