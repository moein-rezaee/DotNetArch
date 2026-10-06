using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace IdentityService.Mcp.Mcp;

[McpServerToolType]
public sealed class IdentityMcpTools(IdentityMcpToolExecutor executor)
{
    [McpServerTool(Name = IdentityMcpToolCatalog.GetSelfProfile, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "self")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.profile.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.profile.read")]
    [Description("Return the authenticated customer's own redacted profile. The server derives the subject; identity selectors are not accepted.")]
    public Task<CallToolResult> GetSelfProfile(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.GetSelfProfileAsync(request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.ListSelfSessions, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "self")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.sessions.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.sessions.read")]
    [Description("List sessions owned by the authenticated customer with privacy-preserving network details.")]
    public Task<CallToolResult> ListSelfSessions(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.ListSelfSessionsAsync(request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.UpdateSelfProfile, ReadOnly = false, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "self")]
    [McpMeta("corevia.risk", "write")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.profile.write")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.profile.write")]
    [Description("Update only the authenticated customer's own profile. The server derives the subject and ignores no identity selector.")]
    public Task<CallToolResult> UpdateSelfProfile(
        [Description("Optional first name.")] string? firstName,
        [Description("Optional last name.")] string? lastName,
        [Description("Optional email address.")] string? email,
        [Description("Optional avatar URL.")] string? avatarUrl,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.UpdateSelfProfileAsync(firstName, lastName, email, avatarUrl, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.RevokeSelfSession, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "atomic")]
    [McpMeta("corevia.audience", "self")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.sessions-revoke.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.sessions-revoke.sensitive")]
    [Description("Revoke one session owned by the authenticated customer with exact server-issued approval.")]
    public Task<CallToolResult> RevokeSelfSession(
        [Description("Session identifier owned by the authenticated customer.")] Guid sessionId,
        [Description("Exact short-lived approval envelope bound to this tool and session.")] string? approval,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.RevokeSelfSessionAsync(sessionId, approval, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.RevokeSelfOtherSessions, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "business")]
    [McpMeta("corevia.audience", "self")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.sessions-revoke-others.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.sessions-revoke-others.sensitive")]
    [Description("Revoke all other sessions of the authenticated customer while preserving the current session when it is known.")]
    public Task<CallToolResult> RevokeSelfOtherSessions(
        [Description("Exact short-lived approval envelope bound to this tool and current session.")] string? approval,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.RevokeSelfOtherSessionsAsync(approval, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.GetUserAccessSummary, ReadOnly = true, Destructive = false, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "business")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "read")]
    [McpMeta("corevia.approvalRequired", false)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.user-access-summary.read")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.user-access-summary.read")]
    [Description("Return a tenant-authorized user's redacted access summary by coordinating the approved Identity Application queries.")]
    public Task<CallToolResult> GetUserAccessSummary(
        [Description("Target user identifier. The server resolves and tenant-authorizes the subject before executing the queries.")] Guid userId,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.GetUserAccessSummaryAsync(userId, request, cancellationToken);

    [McpServerTool(Name = IdentityMcpToolCatalog.RevokeUserSessions, ReadOnly = false, Destructive = true, Idempotent = true)]
    [McpMeta("corevia.version", "0.1.0")]
    [McpMeta("corevia.kind", "business")]
    [McpMeta("corevia.audience", "admin")]
    [McpMeta("corevia.risk", "sensitive")]
    [McpMeta("corevia.approvalRequired", true)]
    [McpMeta("corevia.mcpPermission", "identity.mcp.sessions-admin.sensitive")]
    [McpMeta("corevia.requiredScopes", "identity.mcp.sessions-admin.sensitive")]
    [Description("Revoke one tenant-authorized user's session, or all sessions except the authenticated current session when it belongs to the target user; only an exact server-issued approval envelope is accepted.")]
    public Task<CallToolResult> RevokeUserSessions(
        [Description("Target user identifier resolved and tenant-authorized by the server.")] Guid userId,
        [Description("One session identifier; pass null when allOtherSessions is true.")] Guid? sessionId,
        [Description("Set true to revoke all other sessions; exactly one of sessionId and allOtherSessions must be selected.")] bool allOtherSessions,
        [Description("Exact short-lived approval envelope bound to actor, tenant, tool, action, and resource. Conversational confirmation is not accepted.")] string? approval,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
        => executor.RevokeUserSessionsAsync(userId, sessionId, allOtherSessions, approval, request, cancellationToken);
}
