using L4d2Matchmaking.Contracts;

internal static class ReplyJoinDataSendCoordinator
{
    internal static bool SendAndCapture(
        Func<bool> send,
        AgentOperationRequest? request,
        ulong lobbyId,
        IPlayerEntryEventSink? eventSink,
        DateTimeOffset occurredAtUtc,
        Action<Exception>? onSinkFailure = null)
    {
        var sent = send();
        if (!sent || eventSink is null || request is null)
            return sent;

        try
        {
            var entryEvent = PlayerEntryEventFactory.CreateAfterSuccessfulReply(
                request,
                lobbyId,
                sent,
                occurredAtUtc);
            if (entryEvent is not null)
                eventSink.TryEnqueue(entryEvent);
        }
        catch (Exception exception)
        {
            try
            {
                onSinkFailure?.Invoke(exception);
            }
            catch
            {
                // Sink diagnostics must never change the native send result.
            }
        }

        return sent;
    }
}
