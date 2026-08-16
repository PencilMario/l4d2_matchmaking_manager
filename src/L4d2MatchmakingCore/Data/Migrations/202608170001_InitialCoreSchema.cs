using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608170001_InitialCoreSchema")]
public sealed class InitialCoreSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LobbyOperationAudits",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false).Annotation("Npgsql:ValueGenerationStrategy", "IdentityByDefaultColumn"),
                TargetServerId = table.Column<Guid>(nullable: true),
                WarmupAgentId = table.Column<Guid>(nullable: true),
                WarmupAttemptId = table.Column<Guid>(nullable: true),
                EventType = table.Column<string>(maxLength: 128, nullable: false),
                DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                ObservedAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_LobbyOperationAudits", audit => audit.Id));

        migrationBuilder.CreateTable(
            name: "ReservationLeases",
            columns: table => new
            {
                TargetServerId = table.Column<Guid>(nullable: false),
                OperationId = table.Column<Guid>(nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_ReservationLeases", lease => lease.TargetServerId));

        migrationBuilder.CreateTable(
            name: "SharedLibraryMaintenanceLeases",
            columns: table => new
            {
                Name = table.Column<string>(maxLength: 64, nullable: false),
                OperationId = table.Column<Guid>(nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_SharedLibraryMaintenanceLeases", lease => lease.Name));

        migrationBuilder.CreateTable(
            name: "TargetServers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Host = table.Column<string>(maxLength: 253, nullable: false),
                Port = table.Column<int>(nullable: false),
                RequiresReservation = table.Column<bool>(nullable: false),
                MaxConcurrentWarmups = table.Column<int>(nullable: false, defaultValue: 36),
                AttemptWindowSeconds = table.Column<int>(nullable: false, defaultValue: 720),
                PlayerTarget = table.Column<int>(nullable: false, defaultValue: 6),
                Priority = table.Column<int>(nullable: false, defaultValue: 0),
                Enabled = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_TargetServers", server => server.Id));

        migrationBuilder.CreateTable(
            name: "WarmupAgents",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 128, nullable: false),
                SteamDataVolumeName = table.Column<string>(maxLength: 128, nullable: false),
                AccountConfigVolumeName = table.Column<string>(maxLength: 128, nullable: false),
                DownloadRegion = table.Column<string>(maxLength: 128, nullable: true),
                ContainerId = table.Column<string>(maxLength: 128, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_WarmupAgents", agent => agent.Id));

        migrationBuilder.CreateTable(
            name: "WarmupAttempts",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TargetServerId = table.Column<Guid>(nullable: false),
                WarmupAgentId = table.Column<Guid>(nullable: false),
                OperationId = table.Column<Guid>(nullable: false),
                Mode = table.Column<string>(maxLength: 32, nullable: false),
                State = table.Column<string>(maxLength: 64, nullable: false),
                LobbyId = table.Column<string>(maxLength: 20, nullable: true),
                StartedAt = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: true),
                ObservedAt = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_WarmupAttempts", attempt => attempt.Id));

        migrationBuilder.CreateIndex(name: "IX_LobbyOperationAudits_ObservedAt", table: "LobbyOperationAudits", column: "ObservedAt");
        migrationBuilder.CreateIndex(name: "IX_ReservationLeases_ExpiresAt", table: "ReservationLeases", column: "ExpiresAt");
        migrationBuilder.CreateIndex(name: "IX_WarmupAgents_AccountConfigVolumeName", table: "WarmupAgents", column: "AccountConfigVolumeName", unique: true);
        migrationBuilder.CreateIndex(name: "IX_WarmupAgents_Name", table: "WarmupAgents", column: "Name", unique: true);
        migrationBuilder.CreateIndex(name: "IX_WarmupAgents_SteamDataVolumeName", table: "WarmupAgents", column: "SteamDataVolumeName", unique: true);
        migrationBuilder.CreateIndex(name: "IX_WarmupAttempts_OperationId", table: "WarmupAttempts", column: "OperationId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_WarmupAttempts_TargetServerId_State", table: "WarmupAttempts", columns: new[] { "TargetServerId", "State" });
        migrationBuilder.CreateIndex(name: "IX_WarmupAttempts_WarmupAgentId_State", table: "WarmupAttempts", columns: new[] { "WarmupAgentId", "State" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LobbyOperationAudits");
        migrationBuilder.DropTable(name: "ReservationLeases");
        migrationBuilder.DropTable(name: "SharedLibraryMaintenanceLeases");
        migrationBuilder.DropTable(name: "TargetServers");
        migrationBuilder.DropTable(name: "WarmupAgents");
        migrationBuilder.DropTable(name: "WarmupAttempts");
    }
}
