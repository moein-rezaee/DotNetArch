using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Application.Features.UserTenants.Dtos;
using IdentityService.Application.Features.UserTenants.Queries.GetUserTenants;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Mcp.Mcp;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Mcp.Tests;

internal static class McpTestHelpers
{
    public const string JwtSecret = "identity-mcp-test-jwt-secret-0123456789";
    public const string SigningKey = "identity-mcp-test-signing-key-0123456789";
    public const string Audience = "identity-service-mcp";

    public static IdentityMcpOptions Options(
        string? delegationSigningKey = SigningKey,
        string? approvalSigningKey = SigningKey)
        => new()
        {
            Audience = Audience,
            DelegationSigningKey = delegationSigningKey,
            ApprovalSigningKey = approvalSigningKey
        };

    public static ClaimsPrincipal Principal(
        string actor,
        IEnumerable<string> scopes,
        Guid? tenant = null,
        bool superAdmin = false,
        string? authenticationType = "Test")
    {
        var claims = new List<Claim>
        {
            new("sub", actor),
            new("scope", string.Join(' ', scopes))
        };
        if (tenant.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenant.Value.ToString("D")));
        }
        if (superAdmin)
        {
            claims.Add(new Claim("role", "SuperAdmin"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }

    public static McpExecutionContext Context(
        string actor,
        Guid subject,
        Guid tenant,
        string toolName = IdentityMcpToolCatalog.RevokeUserSessions,
        string risk = "sensitive")
        => new(
            actor,
            subject,
            tenant,
            "admin",
            false,
            Principal(actor, ["identity.mcp.admin.write"], tenant, superAdmin: true),
            ["identity.mcp.admin.write"],
            ["identity.mcp.admin.write"],
            ["identity.mcp.admin.write"],
            toolName,
            "0.1.0",
            "business",
            Audience,
            risk,
            risk == "sensitive",
            "test-correlation");

    public static McpToolRequestInput Input(
        IDictionary<string, JsonElement>? arguments = null,
        string? delegationToken = null)
        => new(arguments, null, delegationToken);

    public static string SignedEnvelope(JsonObject payload, string key)
    {
        var payloadSegment = McpSignedEnvelopeVerifier.Base64UrlEncode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = McpSignedEnvelopeVerifier.Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadSegment)));
        return $"{payloadSegment}.{signature}";
    }

    public static JsonObject DelegationPayload(
        string actor,
        Guid subject,
        Guid tenant,
        string tool,
        string nonce = "delegation-1")
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var allowedScopes = new JsonArray();
        foreach (var scope in IdentityMcpToolCatalog.Active[tool].RequiredScopes)
        {
            allowedScopes.Add(scope);
        }

        return new JsonObject
        {
            ["type"] = "delegation",
            ["actor"] = actor,
            ["subject"] = subject.ToString("D"),
            ["tenant"] = tenant.ToString("D"),
            ["audience"] = Audience,
            ["mode"] = "self",
            ["issuedAt"] = issuedAt.ToString("O"),
            ["expiresAt"] = issuedAt.AddMinutes(2).ToString("O"),
            ["nonce"] = nonce,
            ["allowedTools"] = new JsonArray(tool),
            ["allowedScopes"] = allowedScopes
        };
    }

    public static JsonObject ApprovalPayload(
        string actor,
        Guid tenant,
        string tool,
        string resource,
        string nonce = "approval-1",
        Guid? currentSessionId = null)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var payload = new JsonObject
        {
            ["type"] = "approval",
            ["approvalId"] = "approval-123",
            ["actor"] = actor,
            ["tenant"] = tenant.ToString("D"),
            ["audience"] = Audience,
            ["tool"] = tool,
            ["version"] = "0.1.0",
            ["action"] = "revoke_sessions",
            ["resource"] = resource,
            ["issuedAt"] = issuedAt.ToString("O"),
            ["expiresAt"] = issuedAt.AddMinutes(2).ToString("O"),
            ["nonce"] = nonce
        };

        if (resource.EndsWith("/other-sessions", StringComparison.Ordinal))
        {
            payload["currentSessionId"] = currentSessionId?.ToString("D") ?? string.Empty;
        }

        return payload;
    }

    public static IHttpContextAccessor EmptyHttpContextAccessor() => new HttpContextAccessor();

    public static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().Build();

    public static IReadOnlyCollection<UserTenantDto> Membership(Guid tenantId)
        => [new UserTenantDto(new TenantListItemDto(tenantId, "tenant", "Tenant", true), true)];

    public sealed class StaticPrincipalResolver(ClaimsPrincipal principal) : IMcpPrincipalResolver
    {
        public ClaimsPrincipal? Resolve() => principal;
    }

    public sealed class TestMediator(IReadOnlyCollection<UserTenantDto> memberships) : IMediator
    {
        public int TenantLookupCount { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetUserTenantsQuery)
            {
                TenantLookupCount++;
                return Task.FromResult((TResponse)(object)memberships);
            }

            throw new InvalidOperationException($"Unexpected request: {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new InvalidOperationException($"Unexpected request: {request.GetType().Name}");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"Unexpected request: {request.GetType().Name}");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"Unexpected stream request: {request.GetType().Name}");

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"Unexpected stream request: {request.GetType().Name}");

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
