using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L4d2MatchmakingCore.Data.Migrations;

[DbContext(typeof(MatchmakingDbContext))]
[Migration("202608170004_TargetServerRotationCursor")]
public sealed class TargetServerRotationCursor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TargetServerRotationCursors",
            columns: table => new
            {
                Priority = table.Column<int>(type: "integer", nullable: false),
                LastTargetServerId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_TargetServerRotationCursors", cursor => cursor.Priority));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "TargetServerRotationCursors");
}
