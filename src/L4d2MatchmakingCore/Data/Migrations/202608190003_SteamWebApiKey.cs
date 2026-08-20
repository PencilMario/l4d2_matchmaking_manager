using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608190003_SteamWebApiKey")]
public sealed class SteamWebApiKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "SteamWebApiKeyCiphertext",
        table: "CoreSettings",
        type: "character varying(2048)",
        maxLength: 2048,
        nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "SteamWebApiKeyCiphertext",
        table: "CoreSettings");
}
