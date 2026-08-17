using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Servers;

public sealed class TargetServerObservationService(
    MatchmakingDbContext dbContext,
    TargetServerObservationStore store)
{
    public async Task<IReadOnlyList<TargetServerObservationResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var targetServerIds = await dbContext.TargetServers
            .AsNoTracking()
            .OrderByDescending(server => server.Priority)
            .ThenBy(server => server.CreatedAt)
            .Select(server => server.Id)
            .ToListAsync(cancellationToken);

        return targetServerIds.Select(Project).ToArray();
    }

    private TargetServerObservationResponse Project(Guid targetServerId)
    {
        var snapshot = store.Get(targetServerId);
        if (snapshot is null)
            return new TargetServerObservationResponse(targetServerId, "pending", null, null, null, null);

        if (snapshot.Status == "online")
        {
            return new TargetServerObservationResponse(
                targetServerId,
                "online",
                snapshot.ServerName,
                snapshot.PlayerCount,
                snapshot.MaxPlayers,
                snapshot.ObservedAt);
        }

        return new TargetServerObservationResponse(targetServerId, "unavailable", null, null, null, snapshot.ObservedAt);
    }
}
