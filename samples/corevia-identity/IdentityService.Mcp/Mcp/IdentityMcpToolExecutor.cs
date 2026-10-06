using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityService.Application.Features.ClientScopes.Commands.AddClientScope;
using IdentityService.Application.Features.ClientScopes.Commands.RemoveClientScope;
using IdentityService.Application.Features.ClientScopes.Dtos;
using IdentityService.Application.Features.ClientScopes.Queries.GetClientScopes;
using IdentityService.Application.Features.Clients.Commands.CreateClient;
using IdentityService.Application.Features.Clients.Commands.UpdateClient;
using IdentityService.Application.Features.Clients.Queries.GetClientById;
using IdentityService.Application.Features.Clients.Queries.GetClientsPaged;
using IdentityService.Application.Features.Permissions.Commands.CreatePermission;
using IdentityService.Application.Features.Permissions.Commands.DeletePermission;
using IdentityService.Application.Features.Permissions.Commands.UpdatePermission;
using IdentityService.Application.Features.Permissions.Queries.GetPermissionById;
using IdentityService.Application.Features.Permissions.Queries.GetPermissionsPaged;
using IdentityService.Application.Features.Profile.Commands.UpdateProfile;
using IdentityService.Application.Features.Profile.Models;
using IdentityService.Application.Features.Profile.Queries.GetProfile;
using IdentityService.Application.Features.RolePermissions.Commands.AddRolePermission;
using IdentityService.Application.Features.RolePermissions.Commands.RemoveRolePermission;
using IdentityService.Application.Features.RolePermissions.Dtos;
using IdentityService.Application.Features.RolePermissions.Queries.GetRolePermissions;
using IdentityService.Application.Features.Roles.Commands.CreateRole;
using IdentityService.Application.Features.Roles.Commands.DeleteRole;
using IdentityService.Application.Features.Roles.Commands.UpdateRole;
using IdentityService.Application.Features.Roles.Queries.GetRoleById;
using IdentityService.Application.Features.Roles.Queries.GetRolesPaged;
using IdentityService.Application.Features.Sessions.Commands.RevokeOtherSessions;
using IdentityService.Application.Features.Sessions.Commands.RevokeSession;
using IdentityService.Application.Features.Sessions.Queries.GetCurrentUserSessions;
using IdentityService.Application.Features.ScopePermissions.Commands.AddScopePermission;
using IdentityService.Application.Features.ScopePermissions.Commands.RemoveScopePermission;
using IdentityService.Application.Features.ScopePermissions.Dtos;
using IdentityService.Application.Features.ScopePermissions.Queries.GetScopePermissions;
using IdentityService.Application.Features.Scopes.Commands.CreateScope;
using IdentityService.Application.Features.Scopes.Commands.DeleteScope;
using IdentityService.Application.Features.Scopes.Commands.UpdateScope;
using IdentityService.Application.Features.Scopes.Queries.GetScopeById;
using IdentityService.Application.Features.Scopes.Queries.GetScopesPaged;
using IdentityService.Application.Features.Tenants.Commands.CreateTenant;
using IdentityService.Application.Features.Tenants.Commands.DeleteTenant;
using IdentityService.Application.Features.Tenants.Commands.UpdateTenant;
using IdentityService.Application.Features.Tenants.Queries.GetTenantById;
using IdentityService.Application.Features.Tenants.Queries.GetTenantsPaged;
using IdentityService.Application.Features.UserRoles.Commands.AddUserRole;
using IdentityService.Application.Features.UserRoles.Commands.RemoveUserRole;
using IdentityService.Application.Features.UserRoles.Dtos;
using IdentityService.Application.Features.UserRoles.Queries.GetUserRoles;
using IdentityService.Application.Features.UserTenants.Commands.AddUserTenant;
using IdentityService.Application.Features.UserTenants.Commands.RemoveUserTenant;
using IdentityService.Application.Features.UserTenants.Dtos;
using IdentityService.Application.Features.UserTenants.Queries.GetUserTenants;
using IdentityService.Application.Features.Users.Commands.CreateUser;
using IdentityService.Application.Features.Users.Commands.DeleteUser;
using IdentityService.Application.Features.Users.Commands.UpdateUser;
using IdentityService.Application.Features.Users.Queries.GetUserById;
using IdentityService.Application.Features.Users.Queries.GetUsersPaged;
using MediatR;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace IdentityService.Mcp.Mcp;

public sealed class IdentityMcpToolExecutor(
    IMediator mediator,
    McpExecutionContextResolver contextResolver,
    McpApprovalVerifier approvalVerifier,
    IMcpAuditSink auditSink)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<CallToolResult> GetSelfProfileAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetSelfProfile],
            async context =>
            {
                var result = await mediator.Send(new GetProfileQuery(context.Subject!.Value), cancellationToken);
                return Success(new McpSelfProfileResult(
                    result.Id,
                    MaskPhone(result.PhoneNumber),
                    result.CreatedAt,
                    result.UpdatedAt,
                    result.FirstName,
                    result.LastName,
                    MaskEmail(result.Email),
                    RedactUrl(result.AvatarUrl)));
            },
            cancellationToken);

    public Task<CallToolResult> ListSelfSessionsAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.ListSelfSessions],
            async context =>
            {
                var result = await mediator.Send(new GetCurrentUserSessionsQuery(context.Subject!.Value), cancellationToken);
                var mapped = result.Select(session => new McpSelfSessionResult(
                    session.Id,
                    session.CreatedAt,
                    session.EndedAt,
                    session.DeviceInfo,
                    MaskIpAddress(session.IpAddress),
                    session.IsRevoked));
                return Success(mapped);
            },
            cancellationToken);

    public Task<CallToolResult> UpdateSelfProfileAsync(
        string? firstName,
        string? lastName,
        string? email,
        string? avatarUrl,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.UpdateSelfProfile],
            async context =>
            {
                EnsureArgumentKeys(context, request.Params.Arguments, "firstName", "lastName", "email", "avatarUrl");
                await mediator.Send(
                    new UpdateProfileCommand(
                        context.Subject!.Value,
                        new UpdateProfileRequest(firstName, lastName, email, avatarUrl)),
                    cancellationToken);
                return Success(new { updated = true, userId = context.Subject.Value });
            },
            cancellationToken);

    public Task<CallToolResult> RevokeSelfSessionAsync(
        Guid sessionId,
        string? approval,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.RevokeSelfSession],
            async context =>
            {
                EnsureArgumentKeys(context, request.Params.Arguments, "sessionId", "approval");
                var approvalResult = approvalVerifier.Verify(
                    context,
                    "revoke_sessions",
                    $"user:{context.Subject!.Value:D}/session:{sessionId:D}",
                    approval);
                if (!approvalResult.IsValid)
                {
                    return Error(approvalResult.FailureCode ?? "approval_required", "An exact server-issued approval is required for this sensitive mutation.");
                }

                await mediator.Send(new RevokeSessionCommand(context.Subject.Value, sessionId), cancellationToken);
                return Success(new McpSessionRevocationResult(context.Subject.Value, sessionId, false, true, null));
            },
            cancellationToken);

    public Task<CallToolResult> RevokeSelfOtherSessionsAsync(
        string? approval,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.RevokeSelfOtherSessions],
            async context =>
            {
                EnsureArgumentKeys(context, request.Params.Arguments, "approval");
                var approvalResult = approvalVerifier.Verify(
                    context,
                    "revoke_sessions",
                    $"user:{context.Subject!.Value:D}/other-sessions",
                    approval,
                    context.CurrentSessionId?.ToString("D") ?? string.Empty);
                if (!approvalResult.IsValid)
                {
                    return Error(approvalResult.FailureCode ?? "approval_required", "An exact server-issued approval is required for this sensitive mutation.");
                }

                await mediator.Send(new RevokeOtherSessionsCommand(context.Subject.Value, context.CurrentSessionId), cancellationToken);
                return Success(new McpSessionRevocationResult(context.Subject.Value, null, true, true, context.CurrentSessionId));
            },
            cancellationToken);

    public Task<CallToolResult> ExecuteAdminCapabilityAsync(
        string toolName,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        if (!IdentityMcpToolCatalog.Active.TryGetValue(toolName, out var tool) || !tool.Audience.Equals("admin", StringComparison.Ordinal))
        {
            return Task.FromResult(Error("tool_not_registered", "The requested Identity MCP capability is not registered."));
        }

        return ExecuteAsync(
            request,
            tool,
            context => ExecuteAdminCapabilityAsync(tool, context, request.Params.Arguments, cancellationToken),
            cancellationToken);
    }

    private async Task<CallToolResult> ExecuteAdminCapabilityAsync(
        IdentityMcpToolDescriptor tool,
        McpExecutionContext context,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        var args = arguments ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (tool.ApprovalRequired)
        {
            var approval = approvalVerifier.Verify(
                context,
                tool.Action,
                BuildApprovalResource(tool, args, context),
                ReadString(args, "approval"));
            if (!approval.IsValid)
            {
                return Error(approval.FailureCode ?? "approval_required", "An exact server-issued approval is required for this sensitive mutation.");
            }
        }

        return tool.Name switch
        {
            IdentityMcpToolCatalog.AdminUsersRead => await ReadUsersAsync(context, args, cancellationToken),
            IdentityMcpToolCatalog.AdminUsersWrite => await WriteUsersAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminUsersDelete => await DeleteAsync(new DeleteUserCommand(ReadRequiredGuid(args, "userId")), cancellationToken),
            IdentityMcpToolCatalog.AdminRolesRead => await ReadRolesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminRolesWrite => await WriteRolesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminRolesDelete => await DeleteAsync(new DeleteRoleCommand(ReadRequiredGuid(args, "roleId")), cancellationToken),
            IdentityMcpToolCatalog.AdminTenantsRead => await ReadTenantsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminTenantsWrite => await WriteTenantsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminTenantsDelete => await DeleteAsync(new DeleteTenantCommand(ReadRequiredGuid(args, "tenantId")), cancellationToken),
            IdentityMcpToolCatalog.AdminPermissionsRead => await ReadPermissionsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminPermissionsWrite => await WritePermissionsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminPermissionsDelete => await DeleteAsync(new DeletePermissionCommand(ReadRequiredGuid(args, "permissionId"), ReadString(args, "reason")), cancellationToken),
            IdentityMcpToolCatalog.AdminScopesRead => await ReadScopesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminScopesWrite => await WriteScopesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminScopesDelete => await DeleteAsync(new DeleteScopeCommand(ReadRequiredGuid(args, "scopeId")), cancellationToken),
            IdentityMcpToolCatalog.AdminClientsRead => await ReadClientsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminClientsWrite => await WriteClientsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminUserRolesRead => await ReadUserRolesAsync(context, cancellationToken),
            IdentityMcpToolCatalog.AdminUserRolesWrite => await WriteUserRolesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminUserTenantsRead => await ReadUserTenantsAsync(context, cancellationToken),
            IdentityMcpToolCatalog.AdminUserTenantsWrite => await WriteUserTenantsAsync(context, args, cancellationToken),
            IdentityMcpToolCatalog.AdminRolePermissionsRead => await ReadRolePermissionsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminRolePermissionsWrite => await WriteRolePermissionsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminScopePermissionsRead => await ReadScopePermissionsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminScopePermissionsWrite => await WriteScopePermissionsAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminClientScopesRead => await ReadClientScopesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminClientScopesWrite => await WriteClientScopesAsync(args, cancellationToken),
            IdentityMcpToolCatalog.AdminRoleUsersRead => await ReadRoleUsersAsync(context, args, cancellationToken),
            IdentityMcpToolCatalog.AdminTenantUsersRead => await ReadTenantUsersAsync(context, args, cancellationToken),
            _ => Error("capability_not_implemented", "The registered Identity capability has no Application mapping.")
        };
    }

    private async Task<CallToolResult> ReadUsersAsync(McpExecutionContext context, IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var userId = ReadOptionalGuid(args, "userId");
        object value;
        if (userId.HasValue)
        {
            value = await mediator.Send(new GetUserByIdQuery(userId.Value), cancellationToken);
        }
        else
        {
            value = await mediator.Send(new GetUsersPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize"), null, context.Tenant, null), cancellationToken);
        }

        return SuccessRedacted(value);
    }

    private async Task<CallToolResult> WriteUsersAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var operation = ReadRequiredString(args, "operation");
        if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            var value = await mediator.Send(new CreateUserCommand(new CreateUserRequest(
                ReadRequiredString(args, "phoneNumber"),
                ReadRequiredBool(args, "isActive"))), cancellationToken);
            return SuccessRedacted(value);
        }

        if (operation.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            var value = await mediator.Send(new UpdateUserCommand(
                ReadRequiredGuid(args, "userId"),
                new UpdateUserRequest(ReadRequiredString(args, "phoneNumber"), ReadRequiredBool(args, "isActive"))), cancellationToken);
            return SuccessRedacted(value);
        }

        return Error("invalid_operation", "The users write capability supports only create or update.");
    }

    private async Task<CallToolResult> ReadRolesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var id = ReadOptionalGuid(args, "roleId");
        object value;
        if (id.HasValue)
        {
            value = await mediator.Send(new GetRoleByIdQuery(id.Value), cancellationToken);
        }
        else
        {
            value = await mediator.Send(new GetRolesPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize")), cancellationToken);
        }

        return SuccessRedacted(value);
    }

    private async Task<CallToolResult> WriteRolesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var operation = ReadRequiredString(args, "operation");
        var request = new CreateRoleRequest(ReadRequiredString(args, "name"), ReadRequiredString(args, "displayName"), ReadString(args, "description"));
        if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new CreateRoleCommand(request), cancellationToken));
        }

        if (operation.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new UpdateRoleCommand(ReadRequiredGuid(args, "roleId"), new UpdateRoleRequest(request.Name, request.DisplayName, request.Description)), cancellationToken));
        }

        return Error("invalid_operation", "The roles write capability supports only create or update.");
    }

    private async Task<CallToolResult> ReadTenantsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var id = ReadOptionalGuid(args, "tenantId");
        object value;
        if (id.HasValue)
        {
            value = await mediator.Send(new GetTenantByIdQuery(id.Value), cancellationToken);
        }
        else
        {
            value = await mediator.Send(new GetTenantsPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize")), cancellationToken);
        }

        return SuccessRedacted(value);
    }

    private async Task<CallToolResult> WriteTenantsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var operation = ReadRequiredString(args, "operation");
        var name = ReadRequiredString(args, "name");
        var displayName = ReadRequiredString(args, "displayName");
        var isActive = ReadRequiredBool(args, "isActive");
        if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new CreateTenantCommand(new CreateTenantRequest(name, displayName, isActive)), cancellationToken));
        }

        if (operation.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new UpdateTenantCommand(ReadRequiredGuid(args, "tenantId"), new UpdateTenantRequest(name, displayName, isActive)), cancellationToken));
        }

        return Error("invalid_operation", "The tenants write capability supports only create or update.");
    }

    private async Task<CallToolResult> ReadPermissionsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var id = ReadOptionalGuid(args, "permissionId");
        object value;
        if (id.HasValue)
        {
            value = await mediator.Send(new GetPermissionByIdQuery(id.Value), cancellationToken);
        }
        else
        {
            value = await mediator.Send(new GetPermissionsPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize")), cancellationToken);
        }

        return SuccessRedacted(value);
    }

    private async Task<CallToolResult> WritePermissionsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var operation = ReadRequiredString(args, "operation");
        var key = ReadRequiredString(args, "key");
        var displayName = ReadRequiredString(args, "displayName");
        var description = ReadString(args, "description");
        if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new CreatePermissionCommand(new CreatePermissionRequest(key, displayName, description)), cancellationToken));
        }

        if (operation.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new UpdatePermissionCommand(
                ReadRequiredGuid(args, "permissionId"),
                new UpdatePermissionRequest(key, displayName, description, ReadRequiredBool(args, "isDeprecated"), ReadString(args, "deprecationReason"))), cancellationToken));
        }

        return Error("invalid_operation", "The permissions write capability supports only create or update.");
    }

    private async Task<CallToolResult> ReadScopesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var id = ReadOptionalGuid(args, "scopeId");
        object value;
        if (id.HasValue)
        {
            value = await mediator.Send(new GetScopeByIdQuery(id.Value), cancellationToken);
        }
        else
        {
            value = await mediator.Send(new GetScopesPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize")), cancellationToken);
        }

        return SuccessRedacted(value);
    }

    private async Task<CallToolResult> WriteScopesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var operation = ReadRequiredString(args, "operation");
        var name = ReadRequiredString(args, "name");
        var displayName = ReadRequiredString(args, "displayName");
        var description = ReadString(args, "description");
        if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new CreateScopeCommand(new CreateScopeRequest(name, displayName, description)), cancellationToken));
        }

        if (operation.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new UpdateScopeCommand(ReadRequiredGuid(args, "scopeId"), new UpdateScopeRequest(name, displayName, description)), cancellationToken));
        }

        return Error("invalid_operation", "The scopes write capability supports only create or update.");
    }

    private async Task<CallToolResult> ReadClientsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var id = ReadOptionalGuid(args, "clientId");
        object value;
        if (id.HasValue)
        {
            value = await mediator.Send(new GetClientByIdQuery(id.Value), cancellationToken);
        }
        else
        {
            value = await mediator.Send(new GetClientsPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize")), cancellationToken);
        }

        return SuccessRedacted(value);
    }

    private async Task<CallToolResult> WriteClientsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var operation = ReadRequiredString(args, "operation");
        var name = ReadRequiredString(args, "name");
        var description = ReadString(args, "description");
        var isActive = ReadRequiredBool(args, "isActive");
        if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new CreateClientCommand(new CreateClientRequest(name, description, isActive)), cancellationToken));
        }

        if (operation.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new UpdateClientCommand(ReadRequiredGuid(args, "clientId"), new UpdateClientRequest(name, description, isActive)), cancellationToken));
        }

        return Error("invalid_operation", "The clients write capability supports only create or update.");
    }

    private async Task<CallToolResult> ReadUserRolesAsync(McpExecutionContext context, CancellationToken cancellationToken)
        => SuccessRedacted(await mediator.Send(new GetUserRolesQuery(context.Subject!.Value), cancellationToken));

    private async Task<CallToolResult> WriteUserRolesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var userId = ReadRequiredGuid(args, "userId");
        var roleId = ReadRequiredGuid(args, "roleId");
        var operation = ReadRequiredString(args, "operation");
        if (operation.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new AddUserRoleCommand(userId, new UserRoleRequest(roleId)), cancellationToken));
        }

        if (operation.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return await DeleteAsync(new RemoveUserRoleCommand(userId, roleId), cancellationToken);
        }

        return Error("invalid_operation", "The user-role capability supports only add or remove.");
    }

    private async Task<CallToolResult> ReadUserTenantsAsync(McpExecutionContext context, CancellationToken cancellationToken)
    {
        var mappings = await mediator.Send(new GetUserTenantsQuery(context.Subject!.Value), cancellationToken);
        return SuccessRedacted(mappings.Where(item => item.Tenant.Id == context.Tenant!.Value).ToArray());
    }

    private async Task<CallToolResult> WriteUserTenantsAsync(McpExecutionContext context, IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var userId = ReadRequiredGuid(args, "userId");
        var tenantId = ReadRequiredGuid(args, "tenantId");
        if (tenantId != context.Tenant)
        {
            throw Denied(context, "tenant_boundary_denied", "User tenant membership may only be changed inside the authenticated tenant.");
        }

        var operation = ReadRequiredString(args, "operation");
        if (operation.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new AddUserTenantCommand(userId, new UserTenantRequest(tenantId, ReadRequiredBool(args, "isDefault"))), cancellationToken));
        }

        if (operation.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return await DeleteAsync(new RemoveUserTenantCommand(userId, tenantId), cancellationToken);
        }

        return Error("invalid_operation", "The user-tenant capability supports only add or remove.");
    }

    private async Task<CallToolResult> ReadRolePermissionsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
        => SuccessRedacted(await mediator.Send(new GetRolePermissionsQuery(ReadRequiredGuid(args, "roleId")), cancellationToken));

    private async Task<CallToolResult> WriteRolePermissionsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var roleId = ReadRequiredGuid(args, "roleId");
        var permissionId = ReadRequiredGuid(args, "permissionId");
        var operation = ReadRequiredString(args, "operation");
        if (operation.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new AddRolePermissionCommand(roleId, new RolePermissionRequest(permissionId)), cancellationToken));
        }

        if (operation.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return await DeleteAsync(new RemoveRolePermissionCommand(roleId, permissionId), cancellationToken);
        }

        return Error("invalid_operation", "The role-permission capability supports only add or remove.");
    }

    private async Task<CallToolResult> ReadScopePermissionsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
        => SuccessRedacted(await mediator.Send(new GetScopePermissionsQuery(ReadRequiredGuid(args, "scopeId")), cancellationToken));

    private async Task<CallToolResult> WriteScopePermissionsAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var scopeId = ReadRequiredGuid(args, "scopeId");
        var permissionId = ReadRequiredGuid(args, "permissionId");
        var operation = ReadRequiredString(args, "operation");
        if (operation.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new AddScopePermissionCommand(scopeId, new ScopePermissionRequest(permissionId)), cancellationToken));
        }

        if (operation.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return await DeleteAsync(new RemoveScopePermissionCommand(scopeId, permissionId), cancellationToken);
        }

        return Error("invalid_operation", "The scope-permission capability supports only add or remove.");
    }

    private async Task<CallToolResult> ReadClientScopesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
        => SuccessRedacted(await mediator.Send(new GetClientScopesQuery(ReadRequiredGuid(args, "clientId")), cancellationToken));

    private async Task<CallToolResult> WriteClientScopesAsync(IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var clientId = ReadRequiredGuid(args, "clientId");
        var scopeId = ReadRequiredGuid(args, "scopeId");
        var operation = ReadRequiredString(args, "operation");
        if (operation.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessRedacted(await mediator.Send(new AddClientScopeCommand(clientId, new ClientScopeRequest(scopeId)), cancellationToken));
        }

        if (operation.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return await DeleteAsync(new RemoveClientScopeCommand(clientId, scopeId), cancellationToken);
        }

        return Error("invalid_operation", "The client-scope capability supports only add or remove.");
    }

    private async Task<CallToolResult> ReadRoleUsersAsync(McpExecutionContext context, IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
        => SuccessRedacted(await mediator.Send(new GetUsersPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize"), null, context.Tenant, ReadRequiredGuid(args, "roleId")), cancellationToken));

    private async Task<CallToolResult> ReadTenantUsersAsync(McpExecutionContext context, IDictionary<string, JsonElement> args, CancellationToken cancellationToken)
    {
        var tenantId = ReadRequiredGuid(args, "tenantId");
        if (tenantId != context.Tenant)
        {
            throw Denied(context, "tenant_boundary_denied", "Tenant user listing may only target the authenticated tenant.");
        }

        return SuccessRedacted(await mediator.Send(new GetUsersPagedQuery(ReadPage(args, "pageNumber"), ReadPage(args, "pageSize"), null, tenantId, null), cancellationToken));
    }

    private async Task<CallToolResult> DeleteAsync<TRequest>(TRequest command, CancellationToken cancellationToken)
        where TRequest : IRequest
    {
        await mediator.Send(command, cancellationToken);
        return Success(new { deleted = true });
    }

    private static string BuildApprovalResource(IdentityMcpToolDescriptor tool, IDictionary<string, JsonElement> args, McpExecutionContext context)
    {
        if (!string.IsNullOrWhiteSpace(tool.TargetInputName) && ReadOptionalGuid(args, tool.TargetInputName!) is { } target)
        {
            return $"{tool.Resource}:{target:D}";
        }

        return $"{tool.Resource}:tenant:{context.Tenant:D}";
    }

    private static int ReadPage(IDictionary<string, JsonElement> args, string name)
    {
        if (!args.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return name.Equals("pageSize", StringComparison.Ordinal) ? 20 : 1;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) && result > 0
            ? Math.Min(result, 100)
            : throw new ArgumentException($"{name} must be a positive integer.");
    }

    private static Guid ReadRequiredGuid(IDictionary<string, JsonElement> args, string name)
        => ReadOptionalGuid(args, name) ?? throw new ArgumentException($"{name} is required and must be a valid UUID.");

    private static Guid? ReadOptionalGuid(IDictionary<string, JsonElement> args, string name)
    {
        if (!args.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var result)
            ? result
            : throw new ArgumentException($"{name} must be a valid UUID.");
    }

    private static string ReadRequiredString(IDictionary<string, JsonElement> args, string name)
        => ReadString(args, name) ?? throw new ArgumentException($"{name} is required.");

    private static string? ReadString(IDictionary<string, JsonElement> args, string name)
    {
        if (!args.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new ArgumentException($"{name} must be a string.");
    }

    private static bool ReadRequiredBool(IDictionary<string, JsonElement> args, string name)
    {
        if (!args.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new ArgumentException($"{name} is required and must be boolean.");
        }

        return value.GetBoolean();
    }

    private static CallToolResult SuccessRedacted(object value)
    {
        var node = JsonSerializer.SerializeToNode(value, JsonOptions);
        if (node is not null)
        {
            RedactNode(node);
        }

        return Success(node ?? new JsonObject());
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (IsForbiddenField(property.Key))
                {
                    obj.Remove(property.Key);
                    continue;
                }

                if (property.Value is not null)
                {
                    RedactNode(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array.Where(item => item is not null).Cast<JsonNode>())
            {
                RedactNode(item);
            }
        }
    }

    private static bool IsForbiddenField(string name)
        => name.Contains("token", StringComparison.OrdinalIgnoreCase)
           || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
           || name.Contains("password", StringComparison.OrdinalIgnoreCase)
           || name.Contains("otp", StringComparison.OrdinalIgnoreCase)
           || name.Contains("privateKey", StringComparison.OrdinalIgnoreCase)
           || name.Contains("signingMaterial", StringComparison.OrdinalIgnoreCase)
           || name.Equals("databaseCredential", StringComparison.OrdinalIgnoreCase);

    public Task<CallToolResult> GetUserAccessSummaryAsync(
        Guid userId,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetUserAccessSummary],
            async context =>
            {
                EnsureArgumentKeys(context, request.Params.Arguments, "userId");
                if (context.Subject != userId)
                {
                    throw Denied(context, "target_mismatch", "The server-resolved target does not match the requested operation.");
                }

                var user = await mediator.Send(new GetUserByIdQuery(userId), cancellationToken);
                var roles = await mediator.Send(new GetUserRolesQuery(userId), cancellationToken);
                var tenants = await mediator.Send(new GetUserTenantsQuery(userId), cancellationToken);
                var visibleTenants = tenants
                    .Where(item => item.Tenant.Id == context.Tenant!.Value)
                    .ToArray();
                var result = new McpUserAccessSummaryResult(
                    user.Id,
                    MaskPhone(user.PhoneNumber),
                    user.IsActive,
                    roles.Select(item => new McpRoleResult(item.Role.Id, item.Role.Name, item.Role.DisplayName)).ToArray(),
                    visibleTenants.Select(item => new McpTenantResult(
                        item.Tenant.Id,
                        item.Tenant.Name,
                        item.Tenant.DisplayName,
                        item.Tenant.IsActive,
                        item.IsDefault)).ToArray());
                return Success(result);
            },
            cancellationToken);

    public Task<CallToolResult> RevokeUserSessionsAsync(
        Guid userId,
        Guid? sessionId,
        bool allOtherSessions,
        string? approval,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            request,
            IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.RevokeUserSessions],
            async context =>
            {
                EnsureArgumentKeys(context, request.Params.Arguments, "userId", "sessionId", "allOtherSessions", "approval");
                if (context.Subject != userId)
                {
                    throw Denied(context, "target_mismatch", "The server-resolved target does not match the requested operation.");
                }

                if (allOtherSessions == (sessionId.HasValue))
                {
                    throw Denied(context, "invalid_revocation_scope", "Choose exactly one revocation target: a session or other sessions.");
                }

                var approvalResult = approvalVerifier.Verify(context, userId, sessionId, allOtherSessions, approval);
                if (!approvalResult.IsValid)
                {
                    return Error(approvalResult.FailureCode ?? "approval_required", "An exact server-issued approval is required for this sensitive mutation.");
                }

                if (allOtherSessions)
                {
                    await mediator.Send(new RevokeOtherSessionsCommand(userId, context.CurrentSessionId), cancellationToken);
                }
                else
                {
                    await mediator.Send(new RevokeSessionCommand(userId, sessionId!.Value), cancellationToken);
                }

                return Success(new McpSessionRevocationResult(
                    userId,
                    sessionId,
                    allOtherSessions,
                    true,
                    allOtherSessions ? context.CurrentSessionId : null));
            },
            cancellationToken);

    private async Task<CallToolResult> ExecuteAsync(
        RequestContext<CallToolRequestParams> request,
        IdentityMcpToolDescriptor tool,
        Func<McpExecutionContext, Task<CallToolResult>> operation,
        CancellationToken cancellationToken)
    {
        approvalVerifier.Reset();
        McpExecutionContext? context = null;
        McpApprovalVerification approval = new(false, "not_required", null);
        try
        {
            context = await contextResolver.ResolveAsync(request, tool, cancellationToken);
            var result = await operation(context);
            approval = tool.ApprovalRequired
                ? approvalVerifier.LastVerification
                : approval;

            var authorizationDecision = result.IsError == true && tool.ApprovalRequired && !approval.IsValid
                ? "deny"
                : "allow";
            var resultStatus = result.IsError == true
                ? authorizationDecision == "deny" ? "denied" : "failed"
                : "succeeded";
            await WriteAuditAsync(context, authorizationDecision, approval, resultStatus, null, cancellationToken);
            return result;
        }
        catch (McpAuthorizationException ex)
        {
            approval = tool.ApprovalRequired ? approvalVerifier.LastVerification : approval;
            if (context is not null)
            {
                await WriteAuditAsync(context, "deny", approval, "denied", ex.Code, cancellationToken);
            }
            else
            {
                await WriteAuditAsync(
                    new McpAuditContext(ex.Actor, ex.Subject, ex.Tenant, ex.Mode),
                    tool,
                    "deny",
                    approval,
                    "denied",
                    ex.Code,
                    cancellationToken);
            }
            return Error(ex.Code, ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (context is not null)
            {
                await WriteAuditAsync(context, "allow", approval, "cancelled", "cancelled", cancellationToken);
            }

            throw;
        }
        catch (Exception)
        {
            approval = tool.ApprovalRequired ? approvalVerifier.LastVerification : approval;
            if (context is not null)
            {
                await WriteAuditAsync(context, "allow", approval, "failed", "execution_failed", cancellationToken);
            }

            return Error("execution_failed", "The Identity operation failed without exposing internal details.");
        }
    }

    private Task WriteAuditAsync(
        McpExecutionContext context,
        string decision,
        McpApprovalVerification approval,
        string resultStatus,
        string? failureCode,
        CancellationToken cancellationToken)
        => auditSink.WriteAsync(
            new McpAuditRecord(
                context.Actor,
                context.Subject,
                context.Tenant,
                context.Mode,
                context.ToolName,
                context.ToolVersion,
                context.RequiredScopes,
                context.EffectiveScopes,
                decision,
                approval.ApprovalId,
                approval.Status,
                resultStatus,
                context.CorrelationId,
                DateTimeOffset.UtcNow,
                failureCode),
            cancellationToken);

    private Task WriteAuditAsync(
        McpAuditContext auditContext,
        IdentityMcpToolDescriptor tool,
        string decision,
        McpApprovalVerification approval,
        string resultStatus,
        string failureCode,
        CancellationToken cancellationToken)
        => auditSink.WriteAsync(
            new McpAuditRecord(
                auditContext.Actor,
                auditContext.Subject,
                auditContext.Tenant,
                auditContext.Mode,
                tool.Name,
                tool.Version,
                tool.RequiredScopes,
                [],
                decision,
                approval.ApprovalId,
                approval.Status,
                resultStatus,
                ResolveCorrelationId(),
                DateTimeOffset.UtcNow,
                failureCode),
            cancellationToken);

    private string ResolveCorrelationId() => Guid.NewGuid().ToString("N");

    private static void EnsureArgumentKeys(
        McpExecutionContext context,
        IDictionary<string, JsonElement>? arguments,
        params string[] allowed)
    {
        var allow = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        foreach (var key in arguments?.Keys ?? [])
        {
            if (!allow.Contains(key))
            {
                throw Denied(context, "unexpected_argument", "The tool received an unsupported argument.");
            }
        }
    }

    private static McpAuthorizationException Denied(
        McpExecutionContext context,
        string code,
        string message)
        => new(code, message, context.Actor, context.Subject, context.Tenant, context.Mode);

    private static CallToolResult Success(object value)
        => new()
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, JsonOptions) }],
            IsError = false
        };

    private static CallToolResult Error(string code, string message)
        => new()
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new { error = code, message }, JsonOptions) }],
            IsError = true
        };

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "redacted";
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? "••••" : $"••••{digits[^4..]}";
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        if (!email.Contains('@', StringComparison.Ordinal))
        {
            return "redacted";
        }

        var parts = email.Split('@', 2);
        var local = parts[0];
        return $"{(local.Length == 0 ? "•" : local[0].ToString())}•••@{parts[1]}";
    }

    private static string? RedactUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "redacted";
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.ToString();
    }

    private static string? MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        if (System.Net.IPAddress.TryParse(ipAddress, out var address))
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return $"{bytes[0]}.x.x.{bytes[3]}";
            }

            return "redacted-ipv6";
        }

        return "redacted-ip";
    }

    private sealed record McpAuditContext(string? Actor, Guid? Subject, Guid? Tenant, string? Mode);
}
