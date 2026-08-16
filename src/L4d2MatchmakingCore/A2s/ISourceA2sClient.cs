using System.Net;

namespace L4d2MatchmakingCore.A2s;

public interface ISourceA2sClient
{
    Task<A2sServerInfo> GetInfoAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
}
