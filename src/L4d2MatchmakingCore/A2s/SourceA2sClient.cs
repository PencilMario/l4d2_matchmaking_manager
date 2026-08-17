using System.Net;
using System.Net.Sockets;
using System.Text;

namespace L4d2MatchmakingCore.A2s;

public sealed class SourceA2sClient(TimeSpan timeout) : ISourceA2sClient
{
    private static readonly byte[] Header = [255, 255, 255, 255];
    private static readonly byte[] Query = [255, 255, 255, 255, 0x54, .. Encoding.ASCII.GetBytes("Source Engine Query\0")];

    public async Task<A2sServerInfo> GetInfoAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoint.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("a2s_requires_ipv4_endpoint");
        using var socket = new UdpClient(AddressFamily.InterNetwork);
        socket.Connect(endpoint);
        await socket.SendAsync(Query, cancellationToken);
        var response = await ReceiveAsync(socket, cancellationToken);
        if (IsChallenge(response.Buffer, out var challenge))
        {
            var challengedQuery = new byte[Query.Length + sizeof(int)];
            Query.CopyTo(challengedQuery, 0);
            BitConverter.GetBytes(challenge).CopyTo(challengedQuery, Query.Length);
            await socket.SendAsync(challengedQuery, cancellationToken);
            response = await ReceiveAsync(socket, cancellationToken);
        }
        return ParseInfo(response.Buffer);
    }

    private async Task<UdpReceiveResult> ReceiveAsync(UdpClient socket, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            return await socket.ReceiveAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("a2s_query_timeout");
        }
    }

    private static bool IsChallenge(ReadOnlySpan<byte> packet, out int challenge)
    {
        challenge = 0;
        if (packet.Length != 9 || packet[4] != 0x41 || !packet[..4].SequenceEqual(Header))
            return false;
        challenge = BitConverter.ToInt32(packet[5..]);
        return true;
    }

    private static A2sServerInfo ParseInfo(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 7 || !packet[..4].SequenceEqual(Header) || packet[4] != 0x49)
            throw new InvalidDataException("a2s_invalid_info_response");
        var offset = 6;
        var serverName = ReadCString(packet, ref offset);
        ReadCString(packet, ref offset);
        ReadCString(packet, ref offset);
        ReadCString(packet, ref offset);
        if (offset + 4 > packet.Length)
            throw new InvalidDataException("a2s_truncated_info_response");
        offset += 2;
        var playerCount = packet[offset++];
        var maxPlayers = packet[offset];
        return new A2sServerInfo(serverName, playerCount, maxPlayers, DateTimeOffset.UtcNow);
    }

    private static string ReadCString(ReadOnlySpan<byte> packet, ref int offset)
    {
        var terminator = packet[offset..].IndexOf((byte)0);
        if (terminator < 0)
            throw new InvalidDataException("a2s_truncated_info_response");
        var value = Encoding.UTF8.GetString(packet.Slice(offset, terminator));
        offset += terminator + 1;
        return value;
    }
}
