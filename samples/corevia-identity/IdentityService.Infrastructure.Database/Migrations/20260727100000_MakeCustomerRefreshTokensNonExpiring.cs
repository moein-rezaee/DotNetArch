using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Database.Migrations;

public partial class MakeCustomerRefreshTokensNonExpiring : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "ExpiresAt",
            table: "RefreshTokens",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE \"RefreshTokens\" SET \"ExpiresAt\" = NOW() WHERE \"ExpiresAt\" IS NULL;");

        migrationBuilder.AlterColumn<DateTime>(
            name: "ExpiresAt",
            table: "RefreshTokens",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone",
            oldNullable: true);
    }
}
