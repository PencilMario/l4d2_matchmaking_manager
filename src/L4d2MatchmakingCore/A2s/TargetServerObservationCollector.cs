using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace L4d2MatchmakingCore.A2s;

public sealed class TargetServerObservationCollector(
    IServiceScopeFactory scopeFactory,
    ISourceA2sClient a2s,
    TargetServerObservationStore store) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    public async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        var targets = await dbContext.TargetServers
            .AsNoTracking()
            .Select(server => new TargetEndpoint(server.Id, server.Host, server.Port))
            .ToListAsync(cancellationToken);

        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 16,
            },
            async (target, token) =>
            {
                try
                {
                    var endpoint = await A2sEndpointResolver.ResolveIpv4Async(target.Host, target.Port, token);
                    if (endpoint is null)
                    {
                        store.Replace(target.Id, Unavailable(target.Id));
                        return;
                    }

                    var info = await a2s.GetInfoAsync(endpoint, token);
                    if (info.AppId != 500)
                    {
                        store.Replace(target.Id, Unavailable(target.Id));
                        return;
                    }
                    store.Replace(target.Id, new TargetServerObservation(
                        target.Id,
                        "online",
                        info.ServerName,
                        info.PlayerCount,
                        info.MaxPlayers,
                        info.ObservedAt));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    store.Replace(target.Id, Unavailable(target.Id));
                }
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RefreshOnceAsync(stoppingToken);
    }

    private static TargetServerObservation Unavailable(Guid targetServerId) => new(
        targetServerId,
        "unavailable",
        null,
        null,
        null,
        DateTimeOffset.UtcNow);

    private sealed record TargetEndpoint(Guid Id, string Host, int Port);
}
