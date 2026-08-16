using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Data;

public sealed class MatchmakingDbContext(DbContextOptions<MatchmakingDbContext> options) : DbContext(options)
{
    public DbSet<TargetServer> TargetServers => Set<TargetServer>();
    public DbSet<WarmupAgent> WarmupAgents => Set<WarmupAgent>();
    public DbSet<WarmupAttempt> WarmupAttempts => Set<WarmupAttempt>();
    public DbSet<LobbyOperationAudit> LobbyOperationAudits => Set<LobbyOperationAudit>();
    public DbSet<ReservationLease> ReservationLeases => Set<ReservationLease>();
    public DbSet<SharedLibraryMaintenanceLease> SharedLibraryMaintenanceLeases => Set<SharedLibraryMaintenanceLease>();

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
            entity.Property(audit => audit.EventType).HasMaxLength(128).IsRequired();
            entity.Property(audit => audit.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(audit => audit.ObservedAt);
        });

        modelBuilder.Entity<ReservationLease>(entity =>
        {
            entity.HasKey(lease => lease.TargetServerId);
            entity.HasIndex(lease => lease.ExpiresAt);
        });

        modelBuilder.Entity<SharedLibraryMaintenanceLease>(entity =>
        {
            entity.HasKey(lease => lease.Name);
            entity.Property(lease => lease.Name).HasMaxLength(64);
        });
    }
}
