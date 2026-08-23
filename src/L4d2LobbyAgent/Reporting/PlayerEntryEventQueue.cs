using System.Threading.Channels;
using L4d2Matchmaking.Contracts;

namespace L4d2LobbyAgent.Reporting;

public sealed class PlayerEntryEventQueue : IAsyncDisposable
{
    private readonly Channel<PlayerEntryEvent> _channel;

    public PlayerEntryEventQueue(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _channel = Channel.CreateBounded<PlayerEntryEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    }

    public bool TryEnqueue(PlayerEntryEvent entryEvent) => _channel.Writer.TryWrite(entryEvent);

    public async Task<IReadOnlyList<PlayerEntryEvent>> ReadBatchAsync(int maximum, CancellationToken cancellationToken)
    {
        if (maximum < 1)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (!await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            return Array.Empty<PlayerEntryEvent>();
        var batch = new List<PlayerEntryEvent>(maximum);
        while (batch.Count < maximum && _channel.Reader.TryRead(out var entryEvent))
            batch.Add(entryEvent);
        return batch;
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
