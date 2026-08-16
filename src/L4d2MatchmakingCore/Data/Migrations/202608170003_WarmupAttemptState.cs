using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608170003_WarmupAttemptState")]
public sealed class WarmupAttemptState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Phase", table: "WarmupAttempts", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "awaiting_first_member");
        migrationBuilder.AddColumn<DateTimeOffset>(name: "LobbyReadyAt", table: "WarmupAttempts", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "FirstExternalMemberAt", table: "WarmupAttempts", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "QuietSince", table: "WarmupAttempts", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ExternalMemberIdsJson", table: "WarmupAttempts", type: "jsonb", nullable: false, defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Phase", table: "WarmupAttempts");
        migrationBuilder.DropColumn(name: "LobbyReadyAt", table: "WarmupAttempts");
        migrationBuilder.DropColumn(name: "FirstExternalMemberAt", table: "WarmupAttempts");
        migrationBuilder.DropColumn(name: "QuietSince", table: "WarmupAttempts");
        migrationBuilder.DropColumn(name: "ExternalMemberIdsJson", table: "WarmupAttempts");
    }
}
