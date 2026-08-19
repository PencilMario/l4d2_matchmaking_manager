using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

public partial class WarmupAgentVnc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(
        name: "KeepVncAlive",
        table: "WarmupAgents",
        type: "boolean",
        nullable: false,
        defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "KeepVncAlive",
        table: "WarmupAgents");
}
