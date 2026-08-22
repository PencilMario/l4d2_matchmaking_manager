namespace L4d2MatchmakingCore.Data;

public sealed class TargetServer
{
    public Guid Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool RequiresReservation { get; set; }
    public int MaxConcurrentWarmups { get; set; } = 36;
    public int AttemptWindowSeconds { get; set; } = 720;
    public int PlayerTarget { get; set; } = 6;
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public string? GameMode { get; set; }
    public string? RconPasswordCiphertext { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class WarmupAgent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SteamDataVolumeName { get; set; } = string.Empty;
    public string AccountConfigVolumeName { get; set; } = string.Empty;
    public string? DownloadRegion { get; set; }
    public bool KeepVncAlive { get; set; }
    public string? ContainerId { get; set; }
    public int NoVncPort { get; set; }
    public string Status { get; set; } = "created";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class WarmupAttempt
{
    public Guid Id { get; set; }
    public Guid TargetServerId { get; set; }
    public Guid WarmupAgentId { get; set; }
    public Guid OperationId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? LobbyId { get; set; }
    public string Phase { get; set; } = "awaiting_first_member";
    public DateTimeOffset? LobbyReadyAt { get; set; }
    public DateTimeOffset? FirstExternalMemberAt { get; set; }
    public DateTimeOffset? QuietSince { get; set; }
    public string ExternalMemberIdsJson { get; set; } = "[]";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}

public sealed class LobbyOperationAudit
{
    public long Id { get; set; }
    public Guid? TargetServerId { get; set; }
    public Guid? WarmupAgentId { get; set; }
    public Guid? WarmupAttemptId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public DateTimeOffset ObservedAt { get; set; }
}

public sealed class ReservationLease
{
    public Guid TargetServerId { get; set; }
    public Guid OperationId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class TargetServerRotationCursor
{
    public int Priority { get; set; }
    public Guid LastTargetServerId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SharedLibraryMaintenanceLease
{
    public string Name { get; set; } = "shared-library";
    public Guid OperationId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class CoreSettings
{
    public string Name { get; set; } = "global";
    public string? SteamProxyUrl { get; set; }
    public string? SteamWebApiKeyCiphertext { get; set; }
    public bool WarmupSchedulingEnabled { get; set; } = true;
    public string WarmupPauseWindowsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; }
}
