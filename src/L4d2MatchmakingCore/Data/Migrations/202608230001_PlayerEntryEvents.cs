using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608230001_PlayerEntryEvents")]
public sealed class PlayerEntryEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EntryReportingTokenHash",
            table: "WarmupAgents",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PlayerEntryEvents",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IngestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                LobbyId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                LobbyType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                AgentNameSnapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                DownloadRegionSnapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                TargetServerId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetServerEndpointSnapshot = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                TargetServerNameSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                TargetModeSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_PlayerEntryEvents", entry => entry.EventId));

        migrationBuilder.CreateIndex(
            name: "IX_PlayerEntryEvents_OccurredAtUtc",
            table: "PlayerEntryEvents",
            column: "OccurredAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_PlayerEntryEvents_AgentId_OccurredAtUtc",
            table: "PlayerEntryEvents",
            columns: new[] { "AgentId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_PlayerEntryEvents_TargetServerId_OccurredAtUtc",
            table: "PlayerEntryEvents",
            columns: new[] { "TargetServerId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_PlayerEntryEvents_DownloadRegionSnapshot_OccurredAtUtc",
            table: "PlayerEntryEvents",
            columns: new[] { "DownloadRegionSnapshot", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_PlayerEntryEvents_TargetModeSnapshot_OccurredAtUtc",
            table: "PlayerEntryEvents",
            columns: new[] { "TargetModeSnapshot", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_PlayerEntryEvents_LobbyType_OccurredAtUtc",
            table: "PlayerEntryEvents",
            columns: new[] { "LobbyType", "OccurredAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlayerEntryEvents");
        migrationBuilder.DropColumn(
            name: "EntryReportingTokenHash",
            table: "WarmupAgents");
    }
}
