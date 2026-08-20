namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupSchedulingGate
{
    private readonly SemaphoreSlim mutex = new(1, 1);

    public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            mutex.Release();
        }
    }
}
