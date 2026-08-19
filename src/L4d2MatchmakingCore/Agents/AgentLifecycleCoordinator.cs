using System.Collections.Concurrent;

namespace L4d2MatchmakingCore.Agents;

public sealed class AgentLifecycleCoordinator
{
    private readonly ConcurrentDictionary<Guid, GateState> _gates = [];

    public async Task<T> ExecuteAsync<T>(
        Guid agentId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        GateState state;
        while (true)
        {
            state = _gates.GetOrAdd(agentId, static _ => new GateState());
            if (state.TryAcquire())
                break;
            Retire(agentId, state);
        }

        var entered = false;
        try
        {
            await state.Gate.WaitAsync(cancellationToken);
            entered = true;
            return await operation();
        }
        finally
        {
            if (entered)
                state.Gate.Release();
            if (state.ReleaseLease())
                Retire(agentId, state);
        }
    }

    private void Retire(Guid agentId, GateState state)
    {
        if (_gates.TryRemove(new KeyValuePair<Guid, GateState>(agentId, state)))
            state.Dispose();
    }

    private sealed class GateState : IDisposable
    {
        private readonly object _sync = new();
        private int _leases;
        private bool _retired;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public bool TryAcquire()
        {
            lock (_sync)
            {
                if (_retired)
                    return false;
                _leases++;
                return true;
            }
        }

        public bool ReleaseLease()
        {
            lock (_sync)
            {
                _leases--;
                if (_leases != 0)
                    return false;
                _retired = true;
                return true;
            }
        }

        public void Dispose() => Gate.Dispose();
    }
}
