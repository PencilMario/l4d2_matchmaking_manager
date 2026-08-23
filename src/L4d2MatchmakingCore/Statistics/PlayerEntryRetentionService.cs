using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Statistics;

public sealed class PlayerEntryRetentionService(
    MatchmakingDbContext dbContext,
    Func<DateTimeOffset>? utcNow = null)
{
    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoff = (utcNow?.Invoke() ?? DateTimeOffset.UtcNow).ToUniversalTime().AddDays(-180);
        if (dbContext.Database.IsRelational())
        {
            return await dbContext.PlayerEntryEvents
                .Where(entry => entry.OccurredAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        // The in-memory provider used by deterministic unit tests does not implement
        // ExecuteDeleteAsync; keep the same predicate as a test-only compatibility path.
        var expired = await dbContext.PlayerEntryEvents
            .Where(entry => entry.OccurredAtUtc < cutoff)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
            return 0;
        dbContext.PlayerEntryEvents.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}

public sealed class PlayerEntryRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PlayerEntryRetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var removed = await scope.ServiceProvider
                    .GetRequiredService<PlayerEntryRetentionService>()
                    .CleanupAsync(stoppingToken);
                if (removed > 0)
                    logger.LogInformation("player_entry_events_retention_cleanup removed={Removed}", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "player_entry_events_retention_cleanup_failed");
            }
        }
    }
}
