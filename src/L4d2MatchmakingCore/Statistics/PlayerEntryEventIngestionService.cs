using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace L4d2MatchmakingCore.Statistics;

public sealed class PlayerEntryEventIngestionService(
    MatchmakingDbContext dbContext,
    Func<DateTimeOffset>? utcNow = null)
{
    public async Task<PlayerEntryEventBatchResponse> IngestAsync(
        Guid authenticatedAgentId,
        PlayerEntryEventBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Events is null || request.Events.Count is < 1 or > PlayerEntryStatisticsContract.MaxBatchSize)
            throw new PlayerEntryEventValidationException("player_entry_event_batch_size_invalid");

        var now = (utcNow?.Invoke() ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var duplicates = 0;
        var staged = new Dictionary<Guid, Data.PlayerEntryEvent>();
        foreach (var incoming in request.Events)
        {
            var normalized = Normalize(incoming, authenticatedAgentId, now);
            if (staged.TryGetValue(normalized.EventId, out var stagedExisting))
            {
                if (!SameContent(stagedExisting, normalized))
                    throw new PlayerEntryEventConflictException(normalized.EventId);
                duplicates++;
                continue;
            }

            staged.Add(normalized.EventId, normalized);
        }

        if (staged.Count == 0)
            return new PlayerEntryEventBatchResponse(0, duplicates);

        var existingById = await LoadExistingAsync(staged.Keys, cancellationToken);
        foreach (var pair in staged.ToArray())
        {
            if (!existingById.TryGetValue(pair.Key, out var existing))
                continue;
            if (!SameContent(existing, pair.Value))
                throw new PlayerEntryEventConflictException(pair.Key);
            staged.Remove(pair.Key);
            duplicates++;
        }

        var accepted = staged.Count;
        var uniqueViolationAttempts = 0;
        while (staged.Count > 0)
        {
            dbContext.PlayerEntryEvents.AddRange(staged.Values);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (Exception exception) when (
                IsUniqueConstraintViolation(exception) && uniqueViolationAttempts++ < 3)
            {
                Detach(staged.Keys);
                var concurrentExistingById = await LoadExistingAsync(staged.Keys, cancellationToken);
                foreach (var pair in staged.ToArray())
                {
                    if (!concurrentExistingById.TryGetValue(pair.Key, out var existing))
                        continue;
                    if (!SameContent(existing, pair.Value))
                        throw new PlayerEntryEventConflictException(pair.Key);
                    staged.Remove(pair.Key);
                    accepted--;
                    duplicates++;
                }
            }
        }

        return new PlayerEntryEventBatchResponse(accepted, duplicates);
    }

    private async Task<Dictionary<Guid, Data.PlayerEntryEvent>> LoadExistingAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken) =>
        await dbContext.PlayerEntryEvents
            .Where(entry => eventIds.Contains(entry.EventId))
            .ToDictionaryAsync(entry => entry.EventId, cancellationToken);

    private void Detach(IEnumerable<Guid> eventIds)
    {
        var ids = eventIds.ToHashSet();
        foreach (var tracked in dbContext.ChangeTracker.Entries<Data.PlayerEntryEvent>().ToArray())
        {
            if (ids.Contains(tracked.Entity.EventId))
                tracked.State = EntityState.Detached;
        }
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                return true;
            if (current is ArgumentException && current.Message.Contains(
                    "same key has already been added",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static Data.PlayerEntryEvent Normalize(
        L4d2Matchmaking.Contracts.PlayerEntryEvent incoming,
        Guid authenticatedAgentId,
        DateTimeOffset now)
    {
        if (incoming.EventId == Guid.Empty)
            throw new PlayerEntryEventValidationException("player_entry_event_id_invalid");
        if (incoming.AgentId != authenticatedAgentId)
            throw new PlayerEntryEventValidationException("player_entry_event_agent_mismatch");
        if (incoming.OperationId == Guid.Empty || incoming.TargetServerId == Guid.Empty)
            throw new PlayerEntryEventValidationException("player_entry_event_identity_invalid");

        var occurredAt = incoming.OccurredAtUtc.ToUniversalTime();
        if (occurredAt < now.AddDays(-180) || occurredAt > now.AddMinutes(5))
            throw new PlayerEntryEventValidationException("player_entry_event_time_invalid");

        var lobbyType = incoming.LobbyType?.Trim().ToLowerInvariant();
        if (lobbyType is not (PlayerEntryStatisticsContract.LobbyTypeStandard or PlayerEntryStatisticsContract.LobbyTypeReserved))
            throw new PlayerEntryEventValidationException("player_entry_event_lobby_type_invalid");

        var mode = incoming.TargetModeSnapshot?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(mode) && mode is not (
            PlayerEntryStatisticsContract.GameModeCoop or PlayerEntryStatisticsContract.GameModeVersus))
            throw new PlayerEntryEventValidationException("player_entry_event_mode_invalid");

        var lobbyId = RequireLength(incoming.LobbyId, 20, "lobby_id");
        var agentName = RequireLength(incoming.AgentNameSnapshot, 128, "agent_name_snapshot");
        var endpoint = RequireLength(incoming.TargetServerEndpointSnapshot, 320, "target_server_endpoint_snapshot");
        var targetName = OptionalLength(incoming.TargetServerNameSnapshot, 256, "target_server_name_snapshot");
        var region = OptionalLength(
            PlayerEntryStatisticsContract.NormalizeDownloadRegion(incoming.DownloadRegionSnapshot),
            128,
            "download_region_snapshot");

        return new Data.PlayerEntryEvent
        {
            EventId = incoming.EventId,
            OccurredAtUtc = occurredAt,
            IngestedAtUtc = now,
            OperationId = incoming.OperationId,
            LobbyId = lobbyId,
            LobbyType = lobbyType,
            AgentId = incoming.AgentId,
            AgentNameSnapshot = agentName,
            DownloadRegionSnapshot = region,
            TargetServerId = incoming.TargetServerId,
            TargetServerEndpointSnapshot = endpoint,
            TargetServerNameSnapshot = targetName,
            TargetModeSnapshot = string.IsNullOrWhiteSpace(mode)
                ? PlayerEntryStatisticsContract.GameModeVersus
                : mode,
        };
    }

    private static bool SameContent(Data.PlayerEntryEvent left, Data.PlayerEntryEvent right) =>
        left.EventId == right.EventId &&
        left.OccurredAtUtc == right.OccurredAtUtc &&
        left.OperationId == right.OperationId &&
        left.LobbyId == right.LobbyId &&
        left.LobbyType == right.LobbyType &&
        left.AgentId == right.AgentId &&
        left.AgentNameSnapshot == right.AgentNameSnapshot &&
        left.DownloadRegionSnapshot == right.DownloadRegionSnapshot &&
        left.TargetServerId == right.TargetServerId &&
        left.TargetServerEndpointSnapshot == right.TargetServerEndpointSnapshot &&
        left.TargetServerNameSnapshot == right.TargetServerNameSnapshot &&
        left.TargetModeSnapshot == right.TargetModeSnapshot;

    private static string RequireLength(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new PlayerEntryEventValidationException($"player_entry_event_{fieldName}_required");
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new PlayerEntryEventValidationException($"player_entry_event_{fieldName}_too_long");
        return normalized;
    }

    private static string? OptionalLength(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new PlayerEntryEventValidationException($"player_entry_event_{fieldName}_too_long");
        return normalized;
    }
}

public sealed class PlayerEntryEventValidationException(string code) : InvalidOperationException(code);

public sealed class PlayerEntryEventConflictException(Guid eventId)
    : InvalidOperationException("player_entry_event_id_conflict")
{
    public Guid EventId { get; } = eventId;
}
