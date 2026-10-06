using System.Text.Json.Nodes;

namespace IdentityService.Mcp.Mcp;

public sealed record IdentityMcpToolDescriptor(
    string Name,
    string Version,
    string Kind,
    string Audience,
    string Risk,
    bool ApprovalRequired,
    IReadOnlyCollection<string> RequiredScopes,
    JsonObject InputSchema,
    IReadOnlyCollection<string> ForbiddenInputs,
    string? McpPermission = null,
    string Action = "read",
    string Resource = "identity",
    string? TargetInputName = null,
    bool TargetRequired = true,
    bool RequiresTenantMembership = false,
    IReadOnlyCollection<string>? AllowedInputs = null,
    string Status = "active",
    bool Exposable = true,
    string? ApiPermission = null);

public static class IdentityMcpToolCatalog
{
    public const string GetSelfProfile = "identity_get_self_profile";
    public const string UpdateSelfProfile = "identity_update_self_profile";
    public const string ListSelfSessions = "identity_list_self_sessions";
    public const string RevokeSelfSession = "identity_revoke_self_session";
    public const string RevokeSelfOtherSessions = "identity_revoke_self_other_sessions";
    public const string GetUserAccessSummary = "identity_get_user_access_summary";
    public const string RevokeUserSessions = "identity_revoke_user_sessions";
    public const string AdminUsersRead = "identity_admin_users_read";
    public const string AdminUsersWrite = "identity_admin_users_write";
    public const string AdminUsersDelete = "identity_admin_users_delete";
    public const string AdminRolesRead = "identity_admin_roles_read";
    public const string AdminRolesWrite = "identity_admin_roles_write";
    public const string AdminRolesDelete = "identity_admin_roles_delete";
    public const string AdminTenantsRead = "identity_admin_tenants_read";
    public const string AdminTenantsWrite = "identity_admin_tenants_write";
    public const string AdminTenantsDelete = "identity_admin_tenants_delete";
    public const string AdminPermissionsRead = "identity_admin_permissions_read";
    public const string AdminPermissionsWrite = "identity_admin_permissions_write";
    public const string AdminPermissionsDelete = "identity_admin_permissions_delete";
    public const string AdminScopesRead = "identity_admin_scopes_read";
    public const string AdminScopesWrite = "identity_admin_scopes_write";
    public const string AdminScopesDelete = "identity_admin_scopes_delete";
    public const string AdminClientsRead = "identity_admin_clients_read";
    public const string AdminClientsWrite = "identity_admin_clients_write";
    public const string AdminUserRolesRead = "identity_admin_user_roles_read";
    public const string AdminUserRolesWrite = "identity_admin_user_roles_write";
    public const string AdminUserTenantsRead = "identity_admin_user_tenants_read";
    public const string AdminUserTenantsWrite = "identity_admin_user_tenants_write";
    public const string AdminRolePermissionsRead = "identity_admin_role_permissions_read";
    public const string AdminRolePermissionsWrite = "identity_admin_role_permissions_write";
    public const string AdminScopePermissionsRead = "identity_admin_scope_permissions_read";
    public const string AdminScopePermissionsWrite = "identity_admin_scope_permissions_write";
    public const string AdminClientScopesRead = "identity_admin_client_scopes_read";
    public const string AdminClientScopesWrite = "identity_admin_client_scopes_write";
    public const string AdminRoleUsersRead = "identity_admin_role_users_read";
    public const string AdminTenantUsersRead = "identity_admin_tenant_users_read";
    public const string ProtocolRefresh = "identity_protocol_refresh";
    public const string ProtocolLogout = "identity_protocol_logout";
    public const string ProtocolClientSecretsRead = "identity_protocol_client_secrets_read";
    public const string ProtocolClientSecretsWrite = "identity_protocol_client_secrets_write";
    public const string ProtocolClientSecretsRevoke = "identity_protocol_client_secrets_revoke";

    public static readonly IReadOnlyDictionary<string, IdentityMcpToolDescriptor> All = BuildCatalog();

    public static readonly IReadOnlyDictionary<string, IdentityMcpToolDescriptor> Active =
        All.Where(pair => pair.Value.Status == "active" && pair.Value.Exposable)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static Dictionary<string, IdentityMcpToolDescriptor> BuildCatalog()
    {
        var tools = new Dictionary<string, IdentityMcpToolDescriptor>(StringComparer.Ordinal)
        {
            [GetSelfProfile] = Self(GetSelfProfile, "Identity.Profile.Get", "profile", "read", EmptySchema()),
            [UpdateSelfProfile] = Self(
                UpdateSelfProfile,
                "Identity.Profile.Put",
                "profile",
                "write",
                ObjectSchema(
                    ("firstName", StringSchema()),
                    ("lastName", StringSchema()),
                    ("email", StringSchema()),
                    ("avatarUrl", StringSchema()))),
            [ListSelfSessions] = Self(ListSelfSessions, "Identity.Sessions.Get", "sessions", "read", EmptySchema()),
            [RevokeSelfSession] = Self(
                RevokeSelfSession,
                "Identity.Sessions.Revoke",
                "sessions-revoke",
                "sensitive",
                ObjectSchema(("sessionId", GuidSchema()), ("approval", ApprovalSchema())),
                approvalRequired: true),
            [RevokeSelfOtherSessions] = Self(
                RevokeSelfOtherSessions,
                "Identity.Sessions.RevokeOthers",
                "sessions-revoke-others",
                "sensitive",
                ObjectSchema(("approval", ApprovalSchema())),
                approvalRequired: true),
            [GetUserAccessSummary] = Admin(
                GetUserAccessSummary,
                "Identity.Users.AccessSummary.Get",
                "user-access-summary",
                "read",
                ObjectSchema(("userId", GuidSchema())),
                targetInputName: "userId",
                requiresTenantMembership: true,
                allowedInputs: ["userId"]),
            [RevokeUserSessions] = Admin(
                RevokeUserSessions,
                "Identity.Sessions.AdminRevoke",
                "sessions-admin",
                "sensitive",
                ObjectSchema(
                    ("userId", GuidSchema()),
                    ("sessionId", NullableGuidSchema()),
                    ("allOtherSessions", BoolSchema()),
                    ("approval", ApprovalSchema())),
                approvalRequired: true,
                targetInputName: "userId",
                requiresTenantMembership: true,
                allowedInputs: ["userId", "sessionId", "allOtherSessions", "approval"],
                forbiddenInputs: ["subjectId", "actor", "tenantId"]),
            [AdminUsersRead] = Admin(AdminUsersRead, "Identity.Users.Read", "users", "read", UsersReadSchema(), targetInputName: "userId", targetRequired: false, requiresTenantMembership: true, allowedInputs: ["userId", "pageNumber", "pageSize"]),
            [AdminUsersWrite] = Admin(AdminUsersWrite, "Identity.Users.Write", "users", "write", UsersWriteSchema(), targetInputName: "userId", targetRequired: false, requiresTenantMembership: true, allowedInputs: ["operation", "userId", "phoneNumber", "isActive"]),
            [AdminUsersDelete] = Admin(AdminUsersDelete, "Identity.Users.Delete", "users", "sensitive", TargetSchema(), approvalRequired: true, targetInputName: "userId", requiresTenantMembership: true, allowedInputs: ["userId", "approval"]),
            [AdminRolesRead] = Admin(AdminRolesRead, "Identity.Roles.Read", "roles", "read", PageAndIdSchema("roleId"), targetInputName: "roleId", targetRequired: false, allowedInputs: ["roleId", "pageNumber", "pageSize"]),
            [AdminRolesWrite] = Admin(AdminRolesWrite, "Identity.Roles.Write", "roles", "write", RoleWriteSchema(), allowedInputs: ["operation", "roleId", "name", "displayName", "description"]),
            [AdminRolesDelete] = Admin(AdminRolesDelete, "Identity.Roles.Delete", "roles", "sensitive", TargetSchema("roleId"), approvalRequired: true, targetInputName: "roleId", allowedInputs: ["roleId", "approval"]),
            [AdminTenantsRead] = Admin(AdminTenantsRead, "Identity.Tenants.Read", "tenants", "read", PageAndIdSchema("tenantId"), targetInputName: "tenantId", targetRequired: false, allowedInputs: ["tenantId", "pageNumber", "pageSize"]),
            [AdminTenantsWrite] = Admin(AdminTenantsWrite, "Identity.Tenants.Write", "tenants", "write", TenantWriteSchema(), allowedInputs: ["operation", "tenantId", "name", "displayName", "isActive"]),
            [AdminTenantsDelete] = Admin(AdminTenantsDelete, "Identity.Tenants.Delete", "tenants", "sensitive", TargetSchema("tenantId"), approvalRequired: true, targetInputName: "tenantId", allowedInputs: ["tenantId", "approval"]),
            [AdminPermissionsRead] = Admin(AdminPermissionsRead, "Identity.Permissions.Read", "permissions", "read", PageAndIdSchema("permissionId"), targetInputName: "permissionId", targetRequired: false, allowedInputs: ["permissionId", "pageNumber", "pageSize"]),
            [AdminPermissionsWrite] = Admin(AdminPermissionsWrite, "Identity.Permissions.Write", "permissions", "write", PermissionWriteSchema(), allowedInputs: ["operation", "permissionId", "key", "displayName", "description", "isDeprecated", "deprecationReason"]),
            [AdminPermissionsDelete] = Admin(AdminPermissionsDelete, "Identity.Permissions.Delete", "permissions", "sensitive", TargetSchema("permissionId"), approvalRequired: true, targetInputName: "permissionId", allowedInputs: ["permissionId", "reason", "approval"]),
            [AdminScopesRead] = Admin(AdminScopesRead, "Identity.Scopes.Read", "scopes", "read", PageAndIdSchema("scopeId"), targetInputName: "scopeId", targetRequired: false, allowedInputs: ["scopeId", "pageNumber", "pageSize"]),
            [AdminScopesWrite] = Admin(AdminScopesWrite, "Identity.Scopes.Write", "scopes", "write", ScopeWriteSchema(), allowedInputs: ["operation", "scopeId", "name", "displayName", "description"]),
            [AdminScopesDelete] = Admin(AdminScopesDelete, "Identity.Scopes.Delete", "scopes", "sensitive", TargetSchema("scopeId"), approvalRequired: true, targetInputName: "scopeId", allowedInputs: ["scopeId", "approval"]),
            [AdminClientsRead] = Admin(AdminClientsRead, "Identity.Clients.Read", "clients", "read", PageAndIdSchema("clientId"), targetInputName: "clientId", targetRequired: false, allowedInputs: ["clientId", "pageNumber", "pageSize"]),
            [AdminClientsWrite] = Admin(AdminClientsWrite, "Identity.Clients.Write", "clients", "write", ClientWriteSchema(), allowedInputs: ["operation", "clientId", "name", "description", "isActive"]),
            [AdminUserRolesRead] = Admin(AdminUserRolesRead, "Identity.UserRoles.Read", "user-roles", "read", TargetSchema(), targetInputName: "userId", requiresTenantMembership: true, allowedInputs: ["userId"]),
            [AdminUserRolesWrite] = Admin(AdminUserRolesWrite, "Identity.UserRoles.Write", "user-roles", "sensitive", AssociationSchema("userId", "roleId"), approvalRequired: true, targetInputName: "userId", requiresTenantMembership: true, allowedInputs: ["operation", "userId", "roleId", "approval"]),
            [AdminUserTenantsRead] = Admin(AdminUserTenantsRead, "Identity.UserTenants.Read", "user-tenants", "read", TargetSchema(), targetInputName: "userId", requiresTenantMembership: true, allowedInputs: ["userId"]),
            [AdminUserTenantsWrite] = Admin(AdminUserTenantsWrite, "Identity.UserTenants.Write", "user-tenants", "sensitive", AssociationSchema("userId", "tenantId", includeDefault: true), approvalRequired: true, targetInputName: "userId", requiresTenantMembership: true, allowedInputs: ["operation", "userId", "tenantId", "isDefault", "approval"]),
            [AdminRolePermissionsRead] = Admin(AdminRolePermissionsRead, "Identity.RolePermissions.Read", "role-permissions", "read", TargetSchema("roleId"), targetInputName: "roleId", allowedInputs: ["roleId"]),
            [AdminRolePermissionsWrite] = Admin(AdminRolePermissionsWrite, "Identity.RolePermissions.Write", "role-permissions", "sensitive", AssociationSchema("roleId", "permissionId"), approvalRequired: true, targetInputName: "roleId", allowedInputs: ["operation", "roleId", "permissionId", "approval"]),
            [AdminScopePermissionsRead] = Admin(AdminScopePermissionsRead, "Identity.ScopePermissions.Read", "scope-permissions", "read", TargetSchema("scopeId"), targetInputName: "scopeId", allowedInputs: ["scopeId"]),
            [AdminScopePermissionsWrite] = Admin(AdminScopePermissionsWrite, "Identity.ScopePermissions.Write", "scope-permissions", "sensitive", AssociationSchema("scopeId", "permissionId"), approvalRequired: true, targetInputName: "scopeId", allowedInputs: ["operation", "scopeId", "permissionId", "approval"]),
            [AdminClientScopesRead] = Admin(AdminClientScopesRead, "Identity.ClientScopes.Read", "client-scopes", "read", TargetSchema("clientId"), targetInputName: "clientId", allowedInputs: ["clientId"]),
            [AdminClientScopesWrite] = Admin(AdminClientScopesWrite, "Identity.ClientScopes.Write", "client-scopes", "sensitive", AssociationSchema("clientId", "scopeId"), approvalRequired: true, targetInputName: "clientId", allowedInputs: ["operation", "clientId", "scopeId", "approval"]),
            [AdminRoleUsersRead] = Admin(AdminRoleUsersRead, "Identity.RoleUsers.Read", "role-users", "read", PageAndIdSchema("roleId"), targetInputName: "roleId", allowedInputs: ["roleId", "pageNumber", "pageSize"]),
            [AdminTenantUsersRead] = Admin(AdminTenantUsersRead, "Identity.TenantUsers.Read", "tenant-users", "read", PageAndIdSchema("tenantId"), targetInputName: "tenantId", allowedInputs: ["tenantId", "pageNumber", "pageSize"]),
            [ProtocolRefresh] = DeprecatedProtocol(ProtocolRefresh, "Identity.Token.Refresh", "identity.mcp.protocol.refresh"),
            [ProtocolLogout] = DeprecatedProtocol(ProtocolLogout, "Identity.Token.Logout", "identity.mcp.protocol.logout"),
            [ProtocolClientSecretsRead] = DeprecatedProtocol(ProtocolClientSecretsRead, "Identity.ClientSecrets.Read", "identity.mcp.protocol.client-secrets.read", "client-secrets"),
            [ProtocolClientSecretsWrite] = DeprecatedProtocol(ProtocolClientSecretsWrite, "Identity.ClientSecrets.Write", "identity.mcp.protocol.client-secrets.write", "client-secrets"),
            [ProtocolClientSecretsRevoke] = DeprecatedProtocol(ProtocolClientSecretsRevoke, "Identity.ClientSecrets.Revoke", "identity.mcp.protocol.client-secrets.revoke", "client-secrets")
        };

        return tools;
    }

    private static IdentityMcpToolDescriptor Self(
        string name,
        string apiPermission,
        string resource,
        string risk,
        JsonObject inputSchema,
        bool approvalRequired = false)
        => Descriptor(
            name,
            apiPermission,
            $"identity.mcp.{resource}.{risk}",
            risk == "sensitive" ? "sensitive" : risk,
            "self",
            inputSchema,
            approvalRequired,
            resource,
            null,
            true,
            false,
            inputSchema["properties"] is JsonObject properties ? properties.Select(pair => pair.Key).ToArray() : []);

    private static IdentityMcpToolDescriptor Admin(
        string name,
        string apiPermission,
        string resource,
        string risk,
        JsonObject inputSchema,
        bool approvalRequired = false,
        string? targetInputName = null,
        bool targetRequired = true,
        bool requiresTenantMembership = false,
        IReadOnlyCollection<string>? allowedInputs = null,
        IReadOnlyCollection<string>? forbiddenInputs = null)
        => Descriptor(
            name,
            apiPermission,
            $"identity.mcp.{resource}.{risk}",
            risk == "sensitive" ? "sensitive" : risk,
            "admin",
            inputSchema,
            approvalRequired,
            resource,
            targetInputName,
            targetRequired,
            requiresTenantMembership,
            allowedInputs ?? (inputSchema["properties"] is JsonObject properties ? properties.Select(pair => pair.Key).ToArray() : []),
            forbiddenInputs);

    private static IdentityMcpToolDescriptor DeprecatedProtocol(string name, string apiPermission, string mcpPermission, string resource = "credential-lifecycle")
        => new(
            name,
            "0.1.0",
            "atomic",
            "self",
            "sensitive",
            true,
            [mcpPermission],
            EmptySchema(),
            ["userId", "tenantId", "subjectId", "actor"],
            mcpPermission,
            "sensitive",
            resource,
            null,
            false,
            false,
            [],
            "deprecated",
            false,
            apiPermission);

    private static IdentityMcpToolDescriptor Descriptor(
        string name,
        string apiPermission,
        string mcpPermission,
        string risk,
        string audience,
        JsonObject inputSchema,
        bool approvalRequired,
        string resource,
        string? targetInputName,
        bool targetRequired,
        bool requiresTenantMembership,
        IReadOnlyCollection<string> allowedInputs,
        IReadOnlyCollection<string>? forbiddenInputs = null)
    {
        var kind = name is GetUserAccessSummary or RevokeUserSessions
            ? "business"
            : "atomic";
        IReadOnlyCollection<string> scopes = [mcpPermission];
        IReadOnlyCollection<string> defaultForbidden = audience == "self"
            ? new[] { "userId", "tenantId", "subjectId", "actor" }
            : new[] { "subjectId", "actor" };
        return new IdentityMcpToolDescriptor(
            name,
            "0.1.0",
            kind,
            audience,
            risk,
            approvalRequired,
            scopes,
            inputSchema,
            forbiddenInputs ?? defaultForbidden,
            mcpPermission,
            risk,
            resource,
            targetInputName,
            targetRequired,
            requiresTenantMembership,
            allowedInputs,
            "active",
            true,
            apiPermission);
    }

    private static JsonObject EmptySchema() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["properties"] = new JsonObject()
    };

    private static JsonObject ObjectSchema(params (string Name, JsonObject Schema)[] properties)
    {
        var schema = EmptySchema();
        var propertyObject = (JsonObject)schema["properties"]!;
        foreach (var (name, propertySchema) in properties)
        {
            propertyObject[name] = propertySchema;
        }

        return schema;
    }

    private static JsonObject StringSchema() => new() { ["type"] = "string" };

    private static JsonObject GuidSchema() => new() { ["type"] = "string", ["format"] = "uuid" };

    private static JsonObject NullableGuidSchema() => new() { ["type"] = new JsonArray(JsonValue.Create("string"), JsonValue.Create("null")), ["format"] = "uuid" };

    private static JsonObject BoolSchema() => new() { ["type"] = "boolean" };

    private static JsonObject ApprovalSchema() => new() { ["type"] = "string", ["description"] = "Exact short-lived server-issued approval envelope." };

    private static JsonObject TargetSchema(string idName = "userId") => ObjectSchema((idName, GuidSchema()), ("approval", ApprovalSchema()));

    private static JsonObject PageAndIdSchema(string idName)
        => ObjectSchema((idName, GuidSchema()), ("pageNumber", new JsonObject { ["type"] = "integer", ["minimum"] = 1 }), ("pageSize", new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100 }));

    private static JsonObject UsersReadSchema()
        => ObjectSchema(("userId", GuidSchema()), ("pageNumber", new JsonObject { ["type"] = "integer", ["minimum"] = 1 }), ("pageSize", new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100 }));

    private static JsonObject UsersWriteSchema()
        => ObjectSchema(("operation", StringSchema()), ("userId", GuidSchema()), ("phoneNumber", StringSchema()), ("isActive", BoolSchema()));

    private static JsonObject RoleWriteSchema()
        => ObjectSchema(("operation", StringSchema()), ("roleId", GuidSchema()), ("name", StringSchema()), ("displayName", StringSchema()), ("description", StringSchema()));

    private static JsonObject TenantWriteSchema()
        => ObjectSchema(("operation", StringSchema()), ("tenantId", GuidSchema()), ("name", StringSchema()), ("displayName", StringSchema()), ("isActive", BoolSchema()));

    private static JsonObject PermissionWriteSchema()
        => ObjectSchema(("operation", StringSchema()), ("permissionId", GuidSchema()), ("key", StringSchema()), ("displayName", StringSchema()), ("description", StringSchema()), ("isDeprecated", BoolSchema()), ("deprecationReason", StringSchema()));

    private static JsonObject ScopeWriteSchema()
        => ObjectSchema(("operation", StringSchema()), ("scopeId", GuidSchema()), ("name", StringSchema()), ("displayName", StringSchema()), ("description", StringSchema()));

    private static JsonObject ClientWriteSchema()
        => ObjectSchema(("operation", StringSchema()), ("clientId", GuidSchema()), ("name", StringSchema()), ("description", StringSchema()), ("isActive", BoolSchema()));

    private static JsonObject AssociationSchema(string left, string right, bool includeDefault = false)
    {
        var properties = new List<(string Name, JsonObject Schema)> { (left, GuidSchema()), (right, GuidSchema()), ("operation", StringSchema()), ("approval", ApprovalSchema()) };
        if (includeDefault)
        {
            properties.Add(("isDefault", BoolSchema()));
        }

        return ObjectSchema(properties.ToArray());
    }
}
