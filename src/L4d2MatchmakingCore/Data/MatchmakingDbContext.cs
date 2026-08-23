using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Data;

public sealed class MatchmakingDbContext(DbContextOptions<MatchmakingDbContext> options) : DbContext(options)
{
    public DbSet<TargetServer> TargetServers => Set<TargetServer>();
    public DbSet<WarmupAgent> WarmupAgents => Set<WarmupAgent>();
    public DbSet<WarmupAttempt> WarmupAttempts => Set<WarmupAttempt>();
    public DbSet<LobbyOperationAudit> LobbyOperationAudits => Set<LobbyOperationAudit>();
    public DbSet<PlayerEntryEvent> PlayerEntryEvents => Set<PlayerEntryEvent>();
    public DbSet<ReservationLease> ReservationLeases => Set<ReservationLease>();
    public DbSet<TargetServerRotationCursor> TargetServerRotationCursors => Set<TargetServerRotationCursor>();
    public DbSet<SharedLibraryMaintenanceLease> SharedLibraryMaintenanceLeases => Set<SharedLibraryMaintenanceLease>();
    public DbSet<CoreSettings> CoreSettings => Set<CoreSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TargetServer>(entity =>
        {
            entity.HasKey(server => server.Id);
            entity.Property(server => server.Host).HasMaxLength(253).IsRequired();
            entity.Property(server => server.Port).IsRequired();
            entity.Property(server => server.MaxConcurrentWarmups).HasDefaultValue(36);
            entity.Property(server => server.AttemptWindowSeconds).HasDefaultValue(720);
            entity.Property(server => server.PlayerTarget).HasDefaultValue(6);
            entity.Property(server => server.Priority).HasDefaultValue(0);
            entity.Property(server => server.Enabled).HasDefaultValue(true);
            entity.Property(server => server.GameMode).HasMaxLength(16);
            entity.Property(server => server.RconPasswordCiphertext).HasMaxLength(2048);
        });

        modelBuilder.Entity<WarmupAgent>(entity =>
        {
            entity.HasKey(agent => agent.Id);
            entity.Property(agent => agent.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(agent => agent.Name).IsUnique();
            entity.Property(agent => agent.SteamDataVolumeName).HasMaxLength(128).IsRequired();
            entity.HasIndex(agent => agent.SteamDataVolumeName).IsUnique();
            entity.Property(agent => agent.AccountConfigVolumeName).HasMaxLength(128).IsRequired();
            entity.HasIndex(agent => agent.AccountConfigVolumeName).IsUnique();
            entity.Property(agent => agent.DownloadRegion).HasMaxLength(128);
            entity.Property(agent => agent.EntryReportingTokenHash).HasMaxLength(64);
            entity.Property(agent => agent.KeepVncAlive).HasDefaultValue(false);
            entity.Property(agent => agent.ContainerId).HasMaxLength(128);
            entity.Property(agent => agent.NoVncPort).IsRequired();
            entity.HasIndex(agent => agent.NoVncPort).IsUnique();
            entity.Property(agent => agent.Status).HasMaxLength(32).IsRequired().HasDefaultValue("created");
        });

        modelBuilder.Entity<WarmupAttempt>(entity =>
        {
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Mode).HasMaxLength(32).IsRequired();
            entity.Property(attempt => attempt.State).HasMaxLength(64).IsRequired();
            entity.Property(attempt => attempt.LobbyId).HasMaxLength(20);
            entity.Property(attempt => attempt.Phase).HasMaxLength(32).IsRequired();
            entity.Property(attempt => attempt.ExternalMemberIdsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(attempt => attempt.OperationId).IsUnique();
            entity.HasIndex(attempt => new { attempt.TargetServerId, attempt.State });
            entity.HasIndex(attempt => new { attempt.WarmupAgentId, attempt.State });
        });

        modelBuilder.Entity<LobbyOperationAudit>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Id).UseIdentityByDefaultColumn();
            entity.Property(audit => audit.EventType).HasMaxLength(128).IsRequired();
            entity.Property(audit => audit.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(audit => audit.ObservedAt);
        });

        modelBuilder.Entity<PlayerEntryEvent>(entity =>
        {
            entity.HasKey(entry => entry.EventId);
            entity.Property(entry => entry.LobbyId).HasMaxLength(20).IsRequired();
            entity.Property(entry => entry.LobbyType).HasMaxLength(16).IsRequired();
            entity.Property(entry => entry.AgentNameSnapshot).HasMaxLength(128).IsRequired();
            entity.Property(entry => entry.DownloadRegionSnapshot).HasMaxLength(128);
            entity.Property(entry => entry.TargetServerEndpointSnapshot).HasMaxLength(320).IsRequired();
            entity.Property(entry => entry.TargetServerNameSnapshot).HasMaxLength(256);
            entity.Property(entry => entry.TargetModeSnapshot).HasMaxLength(16).IsRequired();
            entity.HasIndex(entry => entry.OccurredAtUtc);
            entity.HasIndex(entry => new { entry.AgentId, entry.OccurredAtUtc });
            entity.HasIndex(entry => new { entry.TargetServerId, entry.OccurredAtUtc });
            entity.HasIndex(entry => new { entry.DownloadRegionSnapshot, entry.OccurredAtUtc });
            entity.HasIndex(entry => new { entry.TargetModeSnapshot, entry.OccurredAtUtc });
            entity.HasIndex(entry => new { entry.LobbyType, entry.OccurredAtUtc });
        });

        modelBuilder.Entity<ReservationLease>(entity =>
        {
            entity.HasKey(lease => lease.TargetServerId);
            entity.HasIndex(lease => lease.ExpiresAt);
        });

        modelBuilder.Entity<TargetServerRotationCursor>(entity =>
        {
            entity.HasKey(cursor => cursor.Priority);
            entity.Property(cursor => cursor.Priority).ValueGeneratedNever();
            entity.Property(cursor => cursor.LastTargetServerId).IsRequired();
        });

        modelBuilder.Entity<SharedLibraryMaintenanceLease>(entity =>
        {
            entity.HasKey(lease => lease.Name);
            entity.Property(lease => lease.Name).HasMaxLength(64);
        });

        modelBuilder.Entity<CoreSettings>(entity =>
        {
            entity.HasKey(settings => settings.Name);
            entity.Property(settings => settings.Name).HasMaxLength(64);
            entity.Property(settings => settings.SteamProxyUrl).HasMaxLength(2048);
            entity.Property(settings => settings.SteamWebApiKeyCiphertext).HasMaxLength(2048);
            entity.Property(settings => settings.WarmupSchedulingEnabled).HasDefaultValue(true);
            entity.Property(settings => settings.WarmupPauseWindowsJson)
                .HasColumnType("jsonb")
                .IsRequired()
                .HasDefaultValue("[]");
        });
    }
}
