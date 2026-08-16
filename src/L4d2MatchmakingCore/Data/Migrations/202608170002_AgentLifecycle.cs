using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608170002_AgentLifecycle")]
public sealed class AgentLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "NoVncPort",
            table: "WarmupAgents",
            type: "integer",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "WarmupAgents",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "created");
        migrationBuilder.CreateIndex(
            name: "IX_WarmupAgents_NoVncPort",
            table: "WarmupAgents",
            column: "NoVncPort",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_WarmupAgents_NoVncPort", table: "WarmupAgents");
        migrationBuilder.DropColumn(name: "NoVncPort", table: "WarmupAgents");
        migrationBuilder.DropColumn(name: "Status", table: "WarmupAgents");
    }
}
