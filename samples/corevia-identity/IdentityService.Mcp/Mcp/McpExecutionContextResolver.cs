using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityService.Application.Features.UserTenants.Queries.GetUserTenants;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace IdentityService.Mcp.Mcp;

public sealed class McpExecutionContextResolver(
    IMcpPrincipalResolver principalResolver,
    IHttpContextAccessor httpContextAccessor,
    IOptions<IdentityMcpOptions> options,
    IMediator mediator,
    McpSignedEnvelopeVerifier envelopeVerifier,
    IMcpNonceStore nonceStore)
{
    private readonly IdentityMcpOptions _options = options.Value;

    public async Task<McpExecutionContext> ResolveAsync(
        RequestContext<CallToolRequestParams> request,
        IdentityMcpToolDescriptor tool,
        CancellationToken cancellationToken)
        => await ResolveAsync(
            new McpToolRequestInput(
                request.Params.Arguments,
                request.Params.Meta,
                ResolveDelegationToken(request)),
            tool,
            cancellationToken);

    public async Task<McpExecutionContext> ResolveAsync(
        McpToolRequestInput input,
        IdentityMcpToolDescriptor tool,
        CancellationToken cancellationToken)
    {
        var principal = principalResolver.Resolve();
        var actor = GetActor(principal);
        var mode = tool.Audience;
        var correlationId = ResolveCorrelationId();

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw Denied("unauthenticated", "A validated authentication context is required.", actor, mode: mode);
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw Denied("actor_missing", "The authenticated actor identifier is missing.", mode: mode);
        }

        var principalScopes = GetScopes(principal);
        var delegationToken = input.DelegationToken;
        var delegation = TryResolveDelegation(delegationToken, actor, tool, out var delegationFailure);

        if (tool.Audience.Equals("self", StringComparison.Ordinal))
        {
            EnsureNoForbiddenOrUnknownArguments(input.Arguments, tool, tool.Name);

            var selfSubject = ParseGuid(actor);
            var hasPrincipalTenant = TryGetSingleTenant(principal, out var principalTenant, out var tenantFailure);
            var tenant = hasPrincipalTenant
                ? principalTenant
                : null;

            if (delegationToken is not null && delegation is null)
            {
                throw Denied(delegationFailure ?? "invalid_delegation", "The delegated execution context is invalid.", actor, mode: mode);
            }

            var delegated = delegation is not null;
            if (delegated)
            {
                if (string.Equals(tenantFailure, "tenant_context_ambiguous", StringComparison.Ordinal))
                {
                    throw Denied("tenant_context_ambiguous", "The delegated context cannot be bound to an ambiguous tenant context.", actor, mode: mode);
                }

                if (hasPrincipalTenant && principalTenant != delegation!.Tenant)
                {
                    throw Denied("tenant_mismatch", "The delegated tenant does not match the authenticated tenant context.", actor, tenant: principalTenant, mode: mode);
                }

                selfSubject = delegation!.Subject;
                tenant = delegation.Tenant;
            }

            if (selfSubject is null)
            {
                throw Denied("invalid_actor_subject", "The authenticated user identifier is missing or invalid.", actor, mode: mode);
            }

            if (delegated)
            {
                // A signed delegation is necessary but not sufficient: the delegated
                // subject must still be a member of the delegated tenant according to
                // the Identity Application source of truth.
                var subjectTenants = await mediator.Send(new GetUserTenantsQuery(selfSubject.Value), cancellationToken);
                if (!subjectTenants.Any(item => item.Tenant.Id == tenant!.Value))
                {
                    throw Denied(
                        "delegation_tenant_boundary_denied",
                        "The delegated subject is outside the delegated tenant.",
                        actor,
                        selfSubject,
                        tenant,
                        mode);
                }
            }

            var effectiveScopes = IntersectScopes(principalScopes, delegation?.AllowedScopes);
            EnsureRequiredScopes(tool, effectiveScopes, actor, selfSubject, tenant, mode);

            return new McpExecutionContext(
                actor!,
                selfSubject,
                tenant,
                "self",
                delegated,
                principal,
                tool.RequiredScopes,
                principalScopes,
                effectiveScopes,
                tool.Name,
                tool.Version,
                tool.Kind,
                tool.Audience,
                tool.Risk,
                tool.ApprovalRequired,
                correlationId,
                delegated ? null : ResolveCurrentSession(principal, actor!, selfSubject.Value));
        }

        if (!tool.Audience.Equals("admin", StringComparison.Ordinal))
        {
            throw Denied("unsupported_audience", "The requested MCP audience is not enabled.", actor, mode: mode);
        }

        if (delegationToken is not null)
        {
            throw Denied("delegation_mode_not_allowed", "Delegated customer context is only valid for self tools.", actor, mode: mode);
        }

        var tenantId = ResolveRequiredTenant(principal, actor, mode, out var tenantError);
        if (tenantId is null)
        {
            throw Denied(tenantError ?? "tenant_context_required", "A single authenticated tenant context is required.", actor, mode: mode);
        }

        if (!IsAdminActor(principal))
        {
            throw Denied("admin_actor_required", "Only an internal administrative actor can use admin tools.", actor, tenant: tenantId, mode: mode);
        }

        // Check the MCP scope before resolving the target through Application. This keeps
        // unauthorized requests from causing target lookups or revealing target state.
        EnsureRequiredScopes(tool, principalScopes, actor, null, tenantId, mode);
        EnsureAdminArgumentShape(input.Arguments, tool, actor, tenantId, mode);

        Guid? targetSubject = null;
        if (!string.IsNullOrWhiteSpace(tool.TargetInputName))
        {
            targetSubject = tool.TargetRequired
                ? ReadRequiredGuid(input.Arguments, tool.TargetInputName!, actor, tenantId, mode)
                : ReadOptionalGuid(input.Arguments, tool.TargetInputName!, actor, tenantId, mode);
        }

        if (targetSubject.HasValue && tool.RequiresTenantMembership)
        {
            var targetTenants = await mediator.Send(new GetUserTenantsQuery(targetSubject.Value), cancellationToken);
            if (!targetTenants.Any(item => item.Tenant.Id == tenantId.Value))
            {
                throw Denied("tenant_boundary_denied", "The target user is outside the authenticated tenant.", actor, targetSubject, tenantId, mode);
            }
        }

        var currentSessionId = targetSubject.HasValue
            ? ResolveCurrentSession(principal, actor!, targetSubject.Value)
            : null;

        return new McpExecutionContext(
            actor!,
            targetSubject,
            tenantId,
            "admin",
            false,
            principal,
            tool.RequiredScopes,
            principalScopes,
            principalScopes,
            tool.Name,
            tool.Version,
            tool.Kind,
            tool.Audience,
            tool.Risk,
            tool.ApprovalRequired,
            correlationId,
            currentSessionId);
    }

    private McpDelegationGrant? TryResolveDelegation(
        string? compact,
        string? actor,
        IdentityMcpToolDescriptor tool,
        out string? failureCode)
    {
        failureCode = null;
        if (string.IsNullOrWhiteSpace(compact))
        {
            return null;
        }

        if (!envelopeVerifier.TryRead(compact, "delegation", out var payload, out failureCode))
        {
            return null;
        }

        var delegatedActor = McpSignedEnvelopeVerifier.GetString(payload, "actor");
        var audience = McpSignedEnvelopeVerifier.GetString(payload, "audience");
        var delegatedMode = McpSignedEnvelopeVerifier.GetString(payload, "mode");
        var nonce = McpSignedEnvelopeVerifier.GetString(payload, "nonce");
        if (string.IsNullOrWhiteSpace(actor) || !string.Equals(delegatedActor, actor, StringComparison.Ordinal))
        {
            failureCode = "delegation_actor_mismatch";
            return null;
        }

        if (!string.Equals(audience, _options.Audience, StringComparison.Ordinal))
        {
            failureCode = "delegation_audience_mismatch";
            return null;
        }

        if (!string.Equals(delegatedMode, "self", StringComparison.Ordinal))
        {
            failureCode = "delegation_mode_mismatch";
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (!McpSignedEnvelopeVerifier.TryGetGuid(payload, "subject", out var subject) ||
            !McpSignedEnvelopeVerifier.TryGetGuid(payload, "tenant", out var tenant) ||
            !McpSignedEnvelopeVerifier.TryGetTimestamp(payload, "issuedAt", out var issuedAt) ||
            !McpSignedEnvelopeVerifier.TryGetExpiry(payload, out var expiresAt) ||
            issuedAt > now.AddMinutes(1) ||
            issuedAt < now.AddMinutes(-5) ||
            expiresAt <= now ||
            expiresAt > now.AddMinutes(5) ||
            expiresAt <= issuedAt ||
            string.IsNullOrWhiteSpace(nonce))
        {
            failureCode = "delegation_claims_invalid";
            return null;
        }

        var allowedTools = McpSignedEnvelopeVerifier.GetStringArray(payload, "allowedTools");
        var allowedScopes = McpSignedEnvelopeVerifier.GetStringArray(payload, "allowedScopes");
        if (!allowedTools.Contains(tool.Name, StringComparer.Ordinal) ||
            tool.RequiredScopes.Any(scope => !allowedScopes.Contains(scope, StringComparer.Ordinal)))
        {
            failureCode = "delegation_allowlist_denied";
            return null;
        }

        if (!nonceStore.TryConsume("delegation", nonce, expiresAt))
        {
            failureCode = "delegation_replayed";
            return null;
        }

        return new McpDelegationGrant(actor, subject, tenant, expiresAt, allowedTools, allowedScopes);
    }

    private string? ResolveDelegationToken(RequestContext<CallToolRequestParams> request)
    {
        var meta = request.Params.Meta;
        foreach (var key in new[] { "corevia/delegation", "corevia.delegation" })
        {
            if (meta is not null && meta.TryGetPropertyValue(key, out var node) && TryGetString(node, out var value))
            {
                return value;
            }
        }

        var httpContext = httpContextAccessor.HttpContext;
        var header = httpContext?.Request.Headers["X-Corevia-Mcp-Delegation"].FirstOrDefault();
        if (httpContext is not null)
        {
            return header;
        }

        return Environment.GetEnvironmentVariable("COREVIA_MCP_DELEGATION");
    }

    private static bool TryGetString(JsonNode? node, out string? value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var result) ? result : null;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void EnsureNoForbiddenOrUnknownArguments(
        IDictionary<string, JsonElement>? arguments,
        IdentityMcpToolDescriptor tool,
        string toolName)
    {
        var allowed = new HashSet<string>(tool.AllowedInputs ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var key in arguments?.Keys ?? [])
        {
            if (tool.ForbiddenInputs.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                throw Denied("selector_forbidden", $"Tool '{toolName}' does not accept identity selectors.");
            }

            if (!allowed.Contains(key))
            {
                throw Denied("unexpected_argument", $"Tool '{toolName}' received an unsupported argument.");
            }
        }
    }

    private static void EnsureAdminArgumentShape(
        IDictionary<string, JsonElement>? arguments,
        IdentityMcpToolDescriptor tool,
        string? actor,
        Guid? tenant,
        string mode)
    {
        var allowed = new HashSet<string>(tool.AllowedInputs ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var key in arguments?.Keys ?? [])
        {
            if (allowed.Contains(key))
            {
                continue;
            }

            var code = key.Equals("tenantId", StringComparison.OrdinalIgnoreCase) ||
                       key.Equals("subjectId", StringComparison.OrdinalIgnoreCase) ||
                       key.Equals("actor", StringComparison.OrdinalIgnoreCase)
                ? "selector_forbidden"
                : "unexpected_argument";
            throw Denied(code, "Tenant, actor, and subject context are server-resolved and cannot be supplied.", actor, tenant: tenant, mode: mode);
        }
    }

    private static Guid ReadRequiredGuid(
        IDictionary<string, JsonElement>? arguments,
        string name,
        string? actor,
        Guid? tenant,
        string mode)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var result))
        {
            throw Denied("invalid_target", "A valid target user identifier is required.", actor, tenant: tenant, mode: mode);
        }

        return result;
    }

    private static Guid? ReadOptionalGuid(
        IDictionary<string, JsonElement>? arguments,
        string name,
        string? actor,
        Guid? tenant,
        string mode)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var result))
        {
            throw Denied("invalid_target", "The target identifier must be a valid UUID.", actor, tenant: tenant, mode: mode);
        }

        return result;
    }

    private static void EnsureRequiredScopes(
        IdentityMcpToolDescriptor tool,
        IReadOnlyCollection<string> effectiveScopes,
        string? actor,
        Guid? subject,
        Guid? tenant,
        string mode)
    {
        if (tool.RequiredScopes.Any(scope => !effectiveScopes.Contains(scope, StringComparer.Ordinal)))
        {
            throw Denied("scope_denied", "The authenticated context does not grant the required MCP scope.", actor, subject, tenant, mode);
        }
    }

    private static IReadOnlyCollection<string> IntersectScopes(
        IReadOnlyCollection<string> principalScopes,
        IReadOnlyCollection<string>? allowedScopes)
    {
        if (allowedScopes is null)
        {
            return principalScopes.ToArray();
        }

        return principalScopes.Intersect(allowedScopes, StringComparer.Ordinal).ToArray();
    }

    private static string? GetActor(ClaimsPrincipal? principal)
        => principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? principal?.FindFirstValue("sub")
           ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var result) ? result : null;

    private static IReadOnlyCollection<string> GetScopes(ClaimsPrincipal principal)
        => principal.FindAll("scope")
            .Concat(principal.FindAll("scp"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsAdminActor(ClaimsPrincipal principal)
        => principal.FindAll("role").Any(c => string.Equals(c.Value, "SuperAdmin", StringComparison.Ordinal))
           || principal.FindAll(ClaimTypes.Role).Any(c => string.Equals(c.Value, "SuperAdmin", StringComparison.Ordinal))
           || principal.HasClaim(c => c.Type == "client_id")
           || principal.HasClaim(c => c.Type == "token_type" &&
                                      (string.Equals(c.Value, "service", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(c.Value, "client_credentials", StringComparison.OrdinalIgnoreCase)));

    private static Guid? ResolveRequiredTenant(
        ClaimsPrincipal principal,
        string? actor,
        string mode,
        out string? error)
    {
        error = null;
        var values = principal.FindAll("tenant_id")
            .Concat(principal.FindAll("tenant"))
            .Concat(principal.FindAll("tid"))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (values.Length != 1 || !Guid.TryParse(values[0], out var tenant))
        {
            error = values.Length == 0 ? "tenant_context_required" : "tenant_context_ambiguous";
            return null;
        }

        return tenant;
    }

    private static bool TryGetSingleTenant(ClaimsPrincipal principal, out Guid? tenant, out string? error)
    {
        tenant = null;
        error = null;
        var values = principal.FindAll("tenant_id")
            .Concat(principal.FindAll("tenant"))
            .Concat(principal.FindAll("tid"))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (values.Length == 0)
        {
            error = "tenant_context_missing";
            return false;
        }

        if (values.Length != 1 || !Guid.TryParse(values[0], out var parsed))
        {
            error = "tenant_context_ambiguous";
            return false;
        }

        tenant = parsed;
        return true;
    }

    private string ResolveCorrelationId()
        => httpContextAccessor.HttpContext?.TraceIdentifier
           ?? Guid.NewGuid().ToString("N");

    private static Guid? ResolveCurrentSession(ClaimsPrincipal principal, string actor, Guid subject)
    {
        if (!Guid.TryParse(actor, out var actorId) || actorId != subject)
        {
            return null;
        }

        return ParseGuid(
            principal.FindFirstValue("sid")
            ?? principal.FindFirstValue("session_id"));
    }

    private static McpAuthorizationException Denied(
        string code,
        string message,
        string? actor = null,
        Guid? subject = null,
        Guid? tenant = null,
        string? mode = null)
        => new(code, message, actor, subject, tenant, mode);

    private sealed record McpDelegationGrant(
        string Actor,
        Guid Subject,
        Guid Tenant,
        DateTimeOffset ExpiresAt,
        IReadOnlyCollection<string> AllowedTools,
        IReadOnlyCollection<string> AllowedScopes);
}
