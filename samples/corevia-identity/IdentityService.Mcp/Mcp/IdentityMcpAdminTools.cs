using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace IdentityService.Mcp.Mcp;

/// <summary>
/// The administrative surface is deliberately composed of named, domain-level Tools.
/// Each method only supplies the MCP schema; authorization and Application execution stay
/// in <see cref="IdentityMcpToolExecutor"/> and <see cref="McpExecutionContextResolver"/>.
/// </summary>
[McpServerToolType]
public sealed class IdentityMcpAdminTools(IdentityMcpToolExecutor executor)
{
    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUsersRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.users.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.users.read")]
    [Description("Read users in the authenticated tenant, or one tenant-authorized user when userId is supplied.")]
    public Task<CallToolResult> ReadUsers(RequestContext<CallToolRequestParams> request, Guid? userId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUsersRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUsersWrite, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.users.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.users.write")]
    [Description("Create or update a user through Identity Application handlers.")]
    public Task<CallToolResult> WriteUsers(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? userId = null, string? phoneNumber = null, bool? isActive = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUsersWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUsersDelete, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.users.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.users.sensitive")]
    [Description("Delete a tenant-authorized user after exact approval.")]
    public Task<CallToolResult> DeleteUser(RequestContext<CallToolRequestParams> request, Guid userId, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUsersDelete, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminRolesRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.roles.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.roles.read")]
    [Description("Read one role or a paged role directory.")]
    public Task<CallToolResult> ReadRoles(RequestContext<CallToolRequestParams> request, Guid? roleId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminRolesRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminRolesWrite, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.roles.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.roles.write")]
    [Description("Create or update a role through Identity Application handlers.")]
    public Task<CallToolResult> WriteRoles(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? roleId = null, string? name = null, string? displayName = null, string? description = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminRolesWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminRolesDelete, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.roles.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.roles.sensitive")]
    [Description("Delete a role after exact approval.")]
    public Task<CallToolResult> DeleteRole(RequestContext<CallToolRequestParams> request, Guid roleId, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminRolesDelete, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminTenantsRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.tenants.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.tenants.read")]
    [Description("Read one tenant or a paged tenant directory.")]
    public Task<CallToolResult> ReadTenants(RequestContext<CallToolRequestParams> request, Guid? tenantId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminTenantsRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminTenantsWrite, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.tenants.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.tenants.write")]
    [Description("Create or update a tenant through Identity Application handlers.")]
    public Task<CallToolResult> WriteTenants(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? tenantId = null, string? name = null, string? displayName = null, bool? isActive = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminTenantsWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminTenantsDelete, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.tenants.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.tenants.sensitive")]
    [Description("Delete a tenant after exact approval.")]
    public Task<CallToolResult> DeleteTenant(RequestContext<CallToolRequestParams> request, Guid tenantId, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminTenantsDelete, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminPermissionsRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.permissions.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.permissions.read")]
    [Description("Read one permission or a paged permission catalog.")]
    public Task<CallToolResult> ReadPermissions(RequestContext<CallToolRequestParams> request, Guid? permissionId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminPermissionsRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminPermissionsWrite, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.permissions.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.permissions.write")]
    [Description("Create or update a permission through Identity Application handlers.")]
    public Task<CallToolResult> WritePermissions(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? permissionId = null, string? key = null, string? displayName = null, string? description = null, bool? isDeprecated = null, string? deprecationReason = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminPermissionsWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminPermissionsDelete, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.permissions.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.permissions.sensitive")]
    [Description("Delete a permission after exact approval.")]
    public Task<CallToolResult> DeletePermission(RequestContext<CallToolRequestParams> request, Guid permissionId, string? reason = null, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminPermissionsDelete, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminScopesRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.scopes.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.scopes.read")]
    [Description("Read one OAuth scope or a paged scope catalog.")]
    public Task<CallToolResult> ReadScopes(RequestContext<CallToolRequestParams> request, Guid? scopeId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminScopesRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminScopesWrite, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.scopes.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.scopes.write")]
    [Description("Create or update an OAuth scope through Identity Application handlers.")]
    public Task<CallToolResult> WriteScopes(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? scopeId = null, string? name = null, string? displayName = null, string? description = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminScopesWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminScopesDelete, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.scopes.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.scopes.sensitive")]
    [Description("Delete an OAuth scope after exact approval.")]
    public Task<CallToolResult> DeleteScope(RequestContext<CallToolRequestParams> request, Guid scopeId, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminScopesDelete, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminClientsRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.clients.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.clients.read")]
    [Description("Read one client or a paged client directory. Secret values are never returned.")]
    public Task<CallToolResult> ReadClients(RequestContext<CallToolRequestParams> request, Guid? clientId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminClientsRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminClientsWrite, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.clients.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.clients.write")]
    [Description("Create or update an OAuth client through Identity Application handlers. Secret material is not returned.")]
    public Task<CallToolResult> WriteClients(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? clientId = null, string? name = null, string? description = null, bool? isActive = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminClientsWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUserRolesRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.user-roles.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.user-roles.read")]
    [Description("Read roles assigned to a tenant-authorized user.")]
    public Task<CallToolResult> ReadUserRoles(RequestContext<CallToolRequestParams> request, Guid userId, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUserRolesRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUserRolesWrite, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.user-roles.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.user-roles.sensitive")]
    [Description("Add or remove a role assignment for a tenant-authorized user after exact approval.")]
    public Task<CallToolResult> WriteUserRoles(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? userId = null, Guid? roleId = null, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUserRolesWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUserTenantsRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.user-tenants.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.user-tenants.read")]
    [Description("Read the authenticated-tenant membership of a tenant-authorized user.")]
    public Task<CallToolResult> ReadUserTenants(RequestContext<CallToolRequestParams> request, Guid userId, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUserTenantsRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminUserTenantsWrite, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.user-tenants.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.user-tenants.sensitive")]
    [Description("Add or remove a user membership only in the authenticated tenant after exact approval.")]
    public Task<CallToolResult> WriteUserTenants(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? userId = null, Guid? tenantId = null, bool? isDefault = null, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminUserTenantsWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminRolePermissionsRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.role-permissions.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.role-permissions.read")]
    [Description("Read permissions assigned to a role.")]
    public Task<CallToolResult> ReadRolePermissions(RequestContext<CallToolRequestParams> request, Guid roleId, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminRolePermissionsRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminRolePermissionsWrite, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.role-permissions.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.role-permissions.sensitive")]
    [Description("Add or remove a role permission after exact approval.")]
    public Task<CallToolResult> WriteRolePermissions(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? roleId = null, Guid? permissionId = null, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminRolePermissionsWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminScopePermissionsRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.scope-permissions.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.scope-permissions.read")]
    [Description("Read permissions assigned to an OAuth scope.")]
    public Task<CallToolResult> ReadScopePermissions(RequestContext<CallToolRequestParams> request, Guid scopeId, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminScopePermissionsRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminScopePermissionsWrite, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.scope-permissions.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.scope-permissions.sensitive")]
    [Description("Add or remove a scope permission after exact approval.")]
    public Task<CallToolResult> WriteScopePermissions(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? scopeId = null, Guid? permissionId = null, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminScopePermissionsWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminClientScopesRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.client-scopes.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.client-scopes.read")]
    [Description("Read scopes granted to an OAuth client.")]
    public Task<CallToolResult> ReadClientScopes(RequestContext<CallToolRequestParams> request, Guid clientId, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminClientScopesRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminClientScopesWrite, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.client-scopes.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.client-scopes.sensitive")]
    [Description("Add or remove a client scope after exact approval.")]
    public Task<CallToolResult> WriteClientScopes(RequestContext<CallToolRequestParams> request, string? operation = null, Guid? clientId = null, Guid? scopeId = null, string? approval = null, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminClientScopesWrite, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminRoleUsersRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.role-users.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.role-users.read")]
    [Description("Read users assigned to a role.")]
    public Task<CallToolResult> ReadRoleUsers(RequestContext<CallToolRequestParams> request, Guid roleId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminRoleUsersRead, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.AdminTenantUsersRead, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.tenant-users.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.tenant-users.read")]
    [Description("Read users assigned to a tenant.")]
    public Task<CallToolResult> ReadTenantUsers(RequestContext<CallToolRequestParams> request, Guid tenantId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => executor.ExecuteAdminCapabilityAsync(IdentityMcpToolCatalog.AdminTenantUsersRead, request, cancellationToken);
}
