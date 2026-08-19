using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

public partial class GlobalSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CoreSettings",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SteamProxyUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CoreSettings", x => x.Name));
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "CoreSettings");
}
