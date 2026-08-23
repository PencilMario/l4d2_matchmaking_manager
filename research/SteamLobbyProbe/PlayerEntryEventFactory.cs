using L4d2Matchmaking.Contracts;

internal static class PlayerEntryEventFactory
{
    internal static PlayerEntryEvent? CreateAfterSuccessfulReply(
        AgentOperationRequest request,
        ulong lobbyId,
        bool sent,
        DateTimeOffset occurredAtUtc)
    {
        if (!sent || lobbyId == 0 || request.EntryStatisticsContext is not { } context)
            return null;

        return new PlayerEntryEvent(
            Guid.NewGuid(),
            occurredAtUtc,
            request.OperationId,
            lobbyId.ToString(),
            request.Mode == AgentLobbyMode.Reserved
                ? PlayerEntryStatisticsContract.LobbyTypeReserved
                : PlayerEntryStatisticsContract.LobbyTypeStandard,
            context.AgentId,
            context.AgentNameSnapshot,
            PlayerEntryStatisticsContract.NormalizeDownloadRegion(context.ConfiguredDownloadRegionSnapshot),
            context.TargetServerId,
            context.TargetServerEndpointSnapshot,
            context.TargetServerNameSnapshot,
            PlayerEntryStatisticsContract.NormalizeGameMode(context.TargetModeSnapshot));
    }
}
