using L4d2Matchmaking.Contracts;
using Microsoft.Extensions.Logging;

namespace L4d2LobbyAgent.Reporting;

public sealed class PlayerEntryEventSink(
    PlayerEntryEventQueue queue,
    PlayerEntryReportingOptions options,
    ILogger<PlayerEntryEventSink> logger) : IPlayerEntryEventSink
{
    public bool TryEnqueue(PlayerEntryEvent entryEvent)
    {
        if (!options.Enabled)
            return false;
        if (queue.TryEnqueue(entryEvent))
            return true;

        logger.LogWarning("player_entry_event_queue_full");
        return false;
    }
}
