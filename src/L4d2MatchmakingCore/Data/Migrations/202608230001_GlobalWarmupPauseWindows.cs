using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608230001_GlobalWarmupPauseWindows")]
public sealed class GlobalWarmupPauseWindows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "WarmupPauseWindowsJson",
        table: "CoreSettings",
        type: "jsonb",
        nullable: false,
        defaultValue: "[]");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "WarmupPauseWindowsJson",
        table: "CoreSettings");
}
