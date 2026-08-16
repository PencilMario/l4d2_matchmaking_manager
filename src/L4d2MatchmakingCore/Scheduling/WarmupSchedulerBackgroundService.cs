using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupSchedulerBackgroundService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(static scheduler => scheduler.RecoverAsync, stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunAsync(static scheduler => scheduler.TickAsync, stoppingToken);
    }

    private async Task RunAsync(
        Func<WarmupSchedulerService, Func<CancellationToken, Task>> action,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<WarmupSchedulerService>();
        await action(scheduler)(cancellationToken);
    }
}
