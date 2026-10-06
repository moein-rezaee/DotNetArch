using System.Security.Claims;
using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;

namespace IdentityService.Mcp.Mcp;

public sealed record McpToolRequestInput(
    IDictionary<string, JsonElement>? Arguments,
    JsonObject? Meta,
    string? DelegationToken);

public sealed record McpExecutionContext(
    string Actor,
    Guid? Subject,
    Guid? Tenant,
    string Mode,
    bool Delegated,
    ClaimsPrincipal Principal,
    IReadOnlyCollection<string> RequiredScopes,
    IReadOnlyCollection<string> PrincipalScopes,
    IReadOnlyCollection<string> EffectiveScopes,
    string ToolName,
    string ToolVersion,
    string Kind,
    string Audience,
    string Risk,
    bool ApprovalRequired,
    string CorrelationId,
    Guid? CurrentSessionId = null);

/// <summary>
/// MCP authorization denial. Derives from the Kit <see cref="ServiceException"/> (403 Forbidden) so it
/// maps onto the shared error contract; MCP tool execution still translates it into the MCP denial
/// envelope via <see cref="Code"/> and never lets it reach the HTTP exception middleware.
/// </summary>
public sealed class McpAuthorizationException : ServiceException
{
    public McpAuthorizationException(
        string code,
        string message,
        string? actor = null,
        Guid? subject = null,
        Guid? tenant = null,
        string? mode = null)
        : base(message, HttpStatusCode.Forbidden, code)
    {
        Code = code;
        Actor = actor;
        Subject = subject;
        Tenant = tenant;
        Mode = mode;
    }

    public string Code { get; }
    public string? Actor { get; }
    public Guid? Subject { get; }
    public Guid? Tenant { get; }
    public string? Mode { get; }
}

public sealed record McpApprovalVerification(
    bool IsValid,
    string Status,
    string? ApprovalId,
    string? FailureCode = null);
