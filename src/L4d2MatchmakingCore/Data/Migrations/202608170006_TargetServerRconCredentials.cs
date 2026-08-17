using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608170006_TargetServerRconCredentials")]
public sealed class TargetServerRconCredentials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "RconPasswordCiphertext",
            table: "TargetServers",
            maxLength: 2048,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "RconPasswordCiphertext",
            table: "TargetServers");
}
