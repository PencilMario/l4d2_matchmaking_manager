using System.Text.Json;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Servers;

public sealed class TargetServerService(
    MatchmakingDbContext dbContext,
    IRconCredentialProtector credentials,
    WarmupAttemptDrainService drainService)
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
            request.RconPassword,
            request.GameMode);
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
            GameMode = configuration.GameMode,
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
            request.RconPassword,
            request.GameMode);
        server.Host = configuration.Address.Host;
        server.Port = configuration.Address.Port;
        server.RequiresReservation = configuration.RequiresReservation;
        server.Priority = configuration.Priority;
        server.MaxConcurrentWarmups = configuration.MaxConcurrentWarmups;
        server.AttemptWindowSeconds = configuration.AttemptWindowSeconds;
        server.PlayerTarget = configuration.PlayerTarget;
        server.Enabled = configuration.Enabled;
        server.GameMode = configuration.GameMode;
        server.RconPasswordCiphertext = ProtectRconPassword(configuration.RconPassword);
        server.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit("target_server_updated", server.Id, server.UpdatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!server.Enabled && !await drainService.DrainAsync(server.Id, cancellationToken))
            throw new TargetServerDrainException();
        return ToResponse(server);
    }

    public async Task<bool> DeleteAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var server = await dbContext.TargetServers
            .SingleOrDefaultAsync(candidate => candidate.Id == serverId, cancellationToken);
        if (server is null)
            return false;

        var now = DateTimeOffset.UtcNow;
        server.Enabled = false;
        server.UpdatedAt = now;
        AddAudit("target_server_disabled", server.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!await drainService.DrainAsync(server.Id, cancellationToken))
            throw new TargetServerDrainException();
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
        string? rconPassword,
        string? gameMode)
    {
        var requestedConcurrency = maxConcurrentWarmups ?? 36;
        var effectiveConcurrency = requiresReservation ? 1 : requestedConcurrency;
        var effectiveAttemptWindow = attemptWindowSeconds ?? 720;
        var effectivePlayerTarget = playerTarget ?? 6;
        if (requestedConcurrency < 1 || effectiveAttemptWindow < 1 || effectivePlayerTarget < 1)
            throw new ArgumentException("invalid_target_server_configuration");
        if (rconPassword is { Length: 0 })
            throw new ArgumentException("invalid_rcon_password");
        if (!requiresReservation && rconPassword is not null)
            throw new ArgumentException("rcon_requires_reservation");

        var normalizedGameMode = string.IsNullOrWhiteSpace(gameMode) ? null : gameMode.Trim();
        if (normalizedGameMode is not null && normalizedGameMode is not ("coop" or "versus"))
            throw new ArgumentException("invalid_game_mode");

        return new TargetServerConfiguration(
            TargetServerEndpointParser.Parse(endpoint),
            requiresReservation,
            priority ?? 0,
            effectiveConcurrency,
            effectiveAttemptWindow,
            effectivePlayerTarget,
            enabled ?? true,
            requiresReservation ? rconPassword : null,
            normalizedGameMode);
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
        server.UpdatedAt)
    {
        GameMode = server.GameMode,
    };

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
        string? RconPassword,
        string? GameMode);
}

public sealed class TargetServerDrainException : Exception
{
    public TargetServerDrainException() : base("target_server_drain_failed")
    {
    }
}
