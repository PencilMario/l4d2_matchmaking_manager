using System.Collections.Immutable;

namespace L4d2MatchmakingCore.A2s;

public sealed class TargetServerObservationStore
{
    private ImmutableDictionary<Guid, TargetServerObservation> _observations = ImmutableDictionary<Guid, TargetServerObservation>.Empty;

    public TargetServerObservation? Get(Guid targetServerId) =>
        Volatile.Read(ref _observations).TryGetValue(targetServerId, out var observation) ? observation : null;

    public void Replace(Guid targetServerId, TargetServerObservation observation) =>
        ImmutableInterlocked.Update(ref _observations, entries => entries.SetItem(targetServerId, observation));
}
