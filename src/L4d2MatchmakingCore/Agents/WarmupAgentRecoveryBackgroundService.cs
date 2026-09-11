using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace L4d2MatchmakingCore.Agents;

public sealed class WarmupAgentRecoveryBackgroundService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunRecoveryAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunRecoveryAsync(stoppingToken);
    }

    private async Task RunRecoveryAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WarmupAgentService>();
        await service.RecoverPendingAsync(cancellationToken);
    }
}
