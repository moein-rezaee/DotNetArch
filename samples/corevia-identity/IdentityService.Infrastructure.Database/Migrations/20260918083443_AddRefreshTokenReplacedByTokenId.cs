using IdentityService.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Database.Migrations;

// EF Core's MigrateAsync() only discovers a migration via these attributes -- without them
// the migration is silently skipped (no error), which is what happened in production here:
// the app reported "migrations applied successfully" while this column was never created.
[DbContext(typeof(IdentityDbContext))]
[Migration("20260918083443_AddRefreshTokenReplacedByTokenId")]
public partial class AddRefreshTokenReplacedByTokenId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ReplacedByTokenId",
            table: "RefreshTokens",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReplacedByTokenId",
            table: "RefreshTokens");
    }
}
