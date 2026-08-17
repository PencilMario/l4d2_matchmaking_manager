using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Scheduling;

public sealed record WarmupStatusResponse(
    Guid TargetServerId,
    string TargetEndpoint,
    Guid WarmupAgentId,
    string WarmupAgentName,
    Guid OperationId,
    string? LobbyId,
    string Mode,
    string State,
    string Phase,
    DateTimeOffset StartedAt,
    DateTimeOffset? LobbyReadyAt,
    DateTimeOffset? FirstExternalMemberAt,
    DateTimeOffset? QuietSince,
    DateTimeOffset ObservedAt,
    DateTimeOffset Deadline,
    int RemainingSeconds);

public sealed class WarmupStatusService(MatchmakingDbContext dbContext)
{
    public async Task<IReadOnlyList<WarmupStatusResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from attempt in dbContext.WarmupAttempts.AsNoTracking()
            join target in dbContext.TargetServers.AsNoTracking() on attempt.TargetServerId equals target.Id
            join agent in dbContext.WarmupAgents.AsNoTracking() on attempt.WarmupAgentId equals agent.Id
            where attempt.State == "active" || attempt.State == "uncertain"
            orderby attempt.StartedAt, attempt.Id
            select new WarmupStatusRow(
                attempt.TargetServerId,
                $"{target.Host}:{target.Port}",
                attempt.WarmupAgentId,
                agent.Name,
                attempt.OperationId,
                attempt.LobbyId,
                attempt.Mode,
                attempt.State,
                attempt.Phase,
                attempt.StartedAt,
                attempt.LobbyReadyAt,
                attempt.FirstExternalMemberAt,
                attempt.QuietSince,
                attempt.ObservedAt,
                target.AttemptWindowSeconds))
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return rows.Select(row =>
        {
            var deadline = row.StartedAt.AddSeconds(row.AttemptWindowSeconds);
            return new WarmupStatusResponse(
                row.TargetServerId,
                row.TargetEndpoint,
                row.WarmupAgentId,
                row.WarmupAgentName,
                row.OperationId,
                row.LobbyId,
                row.Mode,
                row.State,
                row.Phase,
                row.StartedAt,
                row.LobbyReadyAt,
                row.FirstExternalMemberAt,
                row.QuietSince,
                row.ObservedAt,
                deadline,
                Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds)));
        }).ToList();
    }

    private sealed record WarmupStatusRow(
        Guid TargetServerId,
        string TargetEndpoint,
        Guid WarmupAgentId,
        string WarmupAgentName,
        Guid OperationId,
        string? LobbyId,
        string Mode,
        string State,
        string Phase,
        DateTimeOffset StartedAt,
        DateTimeOffset? LobbyReadyAt,
        DateTimeOffset? FirstExternalMemberAt,
        DateTimeOffset? QuietSince,
        DateTimeOffset ObservedAt,
        int AttemptWindowSeconds);
}
