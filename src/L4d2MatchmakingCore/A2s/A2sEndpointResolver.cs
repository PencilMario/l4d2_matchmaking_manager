using System.Net;
using System.Net.Sockets;

namespace L4d2MatchmakingCore.A2s;

public static class A2sEndpointResolver
{
    public static async Task<IPEndPoint?> ResolveIpv4Async(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork);
            return address is null ? null : new IPEndPoint(address, port);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
