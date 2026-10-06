using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeOrderWriteScopeFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
DO $$
DECLARE
    old_scope_id uuid;
    new_scope_id uuid;
BEGIN
    SELECT ""Id"" INTO old_scope_id
    FROM ""Scopes""
    WHERE ""Name"" = 'order:write'
    LIMIT 1;

    IF old_scope_id IS NULL THEN
        RETURN;
    END IF;

    SELECT ""Id"" INTO new_scope_id
    FROM ""Scopes""
    WHERE ""Name"" = 'order.write'
    LIMIT 1;

    IF new_scope_id IS NULL THEN
        UPDATE ""Scopes""
        SET
            ""Name"" = 'order.write',
            ""DisplayName"" = 'Write Orders',
            ""Description"" = 'M2M scope for BasketService to create orders in OrderService.',
            ""UpdatedAt"" = NOW()
        WHERE ""Id"" = old_scope_id;
    ELSE
        INSERT INTO ""ClientScopes"" (""ClientId"", ""ScopeId"")
        SELECT cs.""ClientId"", new_scope_id
        FROM ""ClientScopes"" cs
        WHERE cs.""ScopeId"" = old_scope_id
        ON CONFLICT (""ClientId"", ""ScopeId"") DO NOTHING;

        DELETE FROM ""ClientScopes""
        WHERE ""ScopeId"" = old_scope_id;

        INSERT INTO ""ScopePermissions"" (""ScopeId"", ""PermissionId"")
        SELECT new_scope_id, sp.""PermissionId""
        FROM ""ScopePermissions"" sp
        WHERE sp.""ScopeId"" = old_scope_id
        ON CONFLICT (""ScopeId"", ""PermissionId"") DO NOTHING;

        DELETE FROM ""ScopePermissions""
        WHERE ""ScopeId"" = old_scope_id;

        DELETE FROM ""Scopes""
        WHERE ""Id"" = old_scope_id;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
DO $$
DECLARE
    old_scope_id uuid;
    new_scope_id uuid;
BEGIN
    SELECT ""Id"" INTO old_scope_id
    FROM ""Scopes""
    WHERE ""Name"" = 'order.write'
    LIMIT 1;

    IF old_scope_id IS NULL THEN
        RETURN;
    END IF;

    SELECT ""Id"" INTO new_scope_id
    FROM ""Scopes""
    WHERE ""Name"" = 'order:write'
    LIMIT 1;

    IF new_scope_id IS NULL THEN
        UPDATE ""Scopes""
        SET
            ""Name"" = 'order:write',
            ""DisplayName"" = 'Write Orders',
            ""Description"" = 'M2M scope for BasketService to create orders in OrderService.',
            ""UpdatedAt"" = NOW()
        WHERE ""Id"" = old_scope_id;
    ELSE
        INSERT INTO ""ClientScopes"" (""ClientId"", ""ScopeId"")
        SELECT cs.""ClientId"", new_scope_id
        FROM ""ClientScopes"" cs
        WHERE cs.""ScopeId"" = old_scope_id
        ON CONFLICT (""ClientId"", ""ScopeId"") DO NOTHING;

        DELETE FROM ""ClientScopes""
        WHERE ""ScopeId"" = old_scope_id;

        INSERT INTO ""ScopePermissions"" (""ScopeId"", ""PermissionId"")
        SELECT new_scope_id, sp.""PermissionId""
        FROM ""ScopePermissions"" sp
        WHERE sp.""ScopeId"" = old_scope_id
        ON CONFLICT (""ScopeId"", ""PermissionId"") DO NOTHING;

        DELETE FROM ""ScopePermissions""
        WHERE ""ScopeId"" = old_scope_id;

        DELETE FROM ""Scopes""
        WHERE ""Id"" = old_scope_id;
    END IF;
END $$;
");
        }
    }
}
