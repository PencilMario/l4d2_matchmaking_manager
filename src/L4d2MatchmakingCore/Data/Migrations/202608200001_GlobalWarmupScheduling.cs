using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608200001_GlobalWarmupScheduling")]
public sealed class GlobalWarmupScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(
        name: "WarmupSchedulingEnabled",
        table: "CoreSettings",
        type: "boolean",
        nullable: false,
        defaultValue: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "WarmupSchedulingEnabled",
        table: "CoreSettings");
}
