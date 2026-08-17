using System.Text.Json;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Servers;

public sealed class TargetServerService(
    MatchmakingDbContext dbContext,
    IRconCredentialProtector credentials)
{
    public async Task<TargetServerResponse> CreateAsync(
        CreateTargetServerRequest request,
        CancellationToken cancellationToken)
    {
        var configuration = Normalize(
            request.Endpoint,
            request.RequiresReservation,
            request.Priority,
            request.MaxConcurrentWarmups,
            request.AttemptWindowSeconds,
            request.PlayerTarget,
            request.Enabled,
            request.RconPassword);
        var now = DateTimeOffset.UtcNow;
        var server = new TargetServer
        {
            Id = Guid.NewGuid(),
            Host = configuration.Address.Host,
            Port = configuration.Address.Port,
            RequiresReservation = configuration.RequiresReservation,
            Priority = configuration.Priority,
            MaxConcurrentWarmups = configuration.MaxConcurrentWarmups,
            AttemptWindowSeconds = configuration.AttemptWindowSeconds,
            PlayerTarget = configuration.PlayerTarget,
            Enabled = configuration.Enabled,
            RconPasswordCiphertext = ProtectRconPassword(configuration.RconPassword),
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.TargetServers.Add(server);
        AddAudit("target_server_created", server.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(server);
    }

    public async Task<IReadOnlyList<TargetServerResponse>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.TargetServers
            .AsNoTracking()
            .OrderByDescending(server => server.Priority)
            .ThenBy(server => server.CreatedAt)
            .Select(server => ToResponse(server))
            .ToListAsync(cancellationToken);

    public async Task<TargetServerResponse?> GetAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var server = await dbContext.TargetServers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == serverId, cancellationToken);
        return server is null ? null : ToResponse(server);
    }

    public async Task<TargetServerResponse?> UpdateAsync(
        Guid serverId,
        UpdateTargetServerRequest request,
        CancellationToken cancellationToken)
    {
        var server = await dbContext.TargetServers
            .SingleOrDefaultAsync(candidate => candidate.Id == serverId, cancellationToken);
        if (server is null)
            return null;

        var configuration = Normalize(
            request.Endpoint,
            request.RequiresReservation,
            request.Priority,
            request.MaxConcurrentWarmups,
            request.AttemptWindowSeconds,
            request.PlayerTarget,
            request.Enabled,
            request.RconPassword);
        server.Host = configuration.Address.Host;
        server.Port = configuration.Address.Port;
        server.RequiresReservation = configuration.RequiresReservation;
        server.Priority = configuration.Priority;
        server.MaxConcurrentWarmups = configuration.MaxConcurrentWarmups;
        server.AttemptWindowSeconds = configuration.AttemptWindowSeconds;
        server.PlayerTarget = configuration.PlayerTarget;
        server.Enabled = configuration.Enabled;
        server.RconPasswordCiphertext = ProtectRconPassword(configuration.RconPassword);
        server.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit("target_server_updated", server.Id, server.UpdatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(server);
    }

    public async Task<bool> DeleteAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var server = await dbContext.TargetServers
            .SingleOrDefaultAsync(candidate => candidate.Id == serverId, cancellationToken);
        if (server is null)
            return false;

        var now = DateTimeOffset.UtcNow;
        dbContext.TargetServers.Remove(server);
        AddAudit("target_server_deleted", server.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void AddAudit(string eventType, Guid targetServerId, DateTimeOffset observedAt) =>
        dbContext.LobbyOperationAudits.Add(new LobbyOperationAudit
        {
            TargetServerId = targetServerId,
            EventType = eventType,
            DetailsJson = JsonSerializer.Serialize(new { targetServerId }),
            ObservedAt = observedAt,
        });

    private static TargetServerConfiguration Normalize(
        string endpoint,
        bool requiresReservation,
        int? priority,
        int? maxConcurrentWarmups,
        int? attemptWindowSeconds,
        int? playerTarget,
        bool? enabled,
        string? rconPassword)
    {
        var requestedConcurrency = maxConcurrentWarmups ?? 36;
        var effectiveConcurrency = requiresReservation ? 1 : requestedConcurrency;
        var effectiveAttemptWindow = attemptWindowSeconds ?? 720;
        var effectivePlayerTarget = playerTarget ?? 6;
        if (requestedConcurrency < 1 || effectiveAttemptWindow < 1 || effectivePlayerTarget < 1)
            throw new ArgumentException("invalid_target_server_configuration");
        if (rconPassword is { Length: 0 })
            throw new ArgumentException("invalid_rcon_password");

        return new TargetServerConfiguration(
            TargetServerEndpointParser.Parse(endpoint),
            requiresReservation,
            priority ?? 0,
            effectiveConcurrency,
            effectiveAttemptWindow,
            effectivePlayerTarget,
            enabled ?? true,
            rconPassword);
    }

    private static TargetServerResponse ToResponse(TargetServer server) => new(
        server.Id,
        $"{server.Host}:{server.Port}",
        server.RequiresReservation,
        server.Priority,
        server.MaxConcurrentWarmups,
        server.AttemptWindowSeconds,
        server.PlayerTarget,
        server.Enabled,
        server.RconPasswordCiphertext is not null,
        server.CreatedAt,
        server.UpdatedAt);

    private string? ProtectRconPassword(string? password)
    {
        if (password is null)
            return null;

        try
        {
            return credentials.Protect(password);
        }
        catch (InvalidOperationException exception) when (
            exception.Message is "rcon_encryption_key_not_configured" or "core_rcon_encryption_key_invalid")
        {
            throw new ArgumentException(exception.Message, exception);
        }
    }

    private sealed record TargetServerConfiguration(
        TargetServerAddress Address,
        bool RequiresReservation,
        int Priority,
        int MaxConcurrentWarmups,
        int AttemptWindowSeconds,
        int PlayerTarget,
        bool Enabled,
        string? RconPassword);
}
