using System.Text.Json;
using IdentityService.Mcp.Mcp;
using Microsoft.Extensions.Options;

namespace IdentityService.Mcp.Tests;

public sealed class McpAuthorizationTests
{
    [Fact]
    public async Task Self_context_uses_authenticated_subject_and_rejects_selector_injection()
    {
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var resolver = CreateResolver(
            McpTestHelpers.Principal(actor.ToString("D"), ["identity.mcp.profile.read"], tenant),
            McpTestHelpers.Membership(tenant));
        var tool = IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetSelfProfile];

        var context = await resolver.ResolveAsync(McpTestHelpers.Input(), tool, CancellationToken.None);

        Assert.Equal(actor, context.Subject);
        Assert.Equal(actor.ToString("D"), context.Actor);
        Assert.Equal(tenant, context.Tenant);
        Assert.Equal("self", context.Mode);
        Assert.False(context.Delegated);

        var injected = McpTestHelpers.Input(new Dictionary<string, JsonElement>
        {
            ["userId"] = JsonSerializer.SerializeToElement(Guid.NewGuid())
        });
        var exception = await Assert.ThrowsAsync<McpAuthorizationException>(() => resolver.ResolveAsync(injected, tool, CancellationToken.None));
        Assert.Equal("selector_forbidden", exception.Code);
    }

    [Fact]
    public async Task Delegated_context_is_signed_short_lived_tenant_bound_and_scope_intersected()
    {
        var actor = "customer-agent";
        var subject = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var payload = McpTestHelpers.DelegationPayload(
            actor,
            subject,
            tenant,
            IdentityMcpToolCatalog.GetSelfProfile);
        var token = McpTestHelpers.SignedEnvelope(payload, McpTestHelpers.SigningKey);
        var resolver = CreateResolver(
            McpTestHelpers.Principal(actor, ["identity.mcp.profile.read"], tenant),
            McpTestHelpers.Membership(tenant));
        var tool = IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetSelfProfile];

        var context = await resolver.ResolveAsync(McpTestHelpers.Input(delegationToken: token), tool, CancellationToken.None);

        Assert.True(context.Delegated);
        Assert.Equal(subject, context.Subject);
        Assert.Equal(tenant, context.Tenant);
        Assert.Equal(["identity.mcp.profile.read"], context.EffectiveScopes);

        var replay = await Assert.ThrowsAsync<McpAuthorizationException>(() => resolver.ResolveAsync(McpTestHelpers.Input(delegationToken: token), tool, CancellationToken.None));
        Assert.Equal("delegation_replayed", replay.Code);
    }

    [Fact]
    public async Task Delegated_context_rejects_subject_outside_signed_tenant()
    {
        var actor = "customer-agent";
        var subject = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var payload = McpTestHelpers.DelegationPayload(
            actor,
            subject,
            tenant,
            IdentityMcpToolCatalog.GetSelfProfile,
            nonce: "delegation-cross-tenant");
        var token = McpTestHelpers.SignedEnvelope(payload, McpTestHelpers.SigningKey);
        var resolver = CreateResolver(
            McpTestHelpers.Principal(actor, ["identity.mcp.profile.read"], tenant),
            mediator: new McpTestHelpers.TestMediator(McpTestHelpers.Membership(otherTenant)));
        var tool = IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetSelfProfile];

        var exception = await Assert.ThrowsAsync<McpAuthorizationException>(() =>
            resolver.ResolveAsync(McpTestHelpers.Input(delegationToken: token), tool, CancellationToken.None));

        Assert.Equal("delegation_tenant_boundary_denied", exception.Code);
    }

    [Fact]
    public async Task Admin_context_checks_scope_before_target_lookup_and_enforces_tenant_boundary()
    {
        var actor = Guid.NewGuid().ToString("D");
        var target = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var mediator = new McpTestHelpers.TestMediator(McpTestHelpers.Membership(otherTenant));
        var resolver = CreateResolver(
            McpTestHelpers.Principal(actor, [], tenant, superAdmin: true),
            mediator: mediator);
        var tool = IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetUserAccessSummary];
        var input = McpTestHelpers.Input(new Dictionary<string, JsonElement>
        {
            ["userId"] = JsonSerializer.SerializeToElement(target)
        });

        var scopeDenied = await Assert.ThrowsAsync<McpAuthorizationException>(() => resolver.ResolveAsync(input, tool, CancellationToken.None));
        Assert.Equal("scope_denied", scopeDenied.Code);
        Assert.Equal(0, mediator.TenantLookupCount);

        var authorizedResolver = CreateResolver(
            McpTestHelpers.Principal(actor, ["identity.mcp.user-access-summary.read"], tenant, superAdmin: true),
            mediator: mediator);
        var boundaryDenied = await Assert.ThrowsAsync<McpAuthorizationException>(() => authorizedResolver.ResolveAsync(input, tool, CancellationToken.None));
        Assert.Equal("tenant_boundary_denied", boundaryDenied.Code);
        Assert.Equal(1, mediator.TenantLookupCount);
    }

    [Fact]
    public async Task Current_session_is_server_resolved_only_when_actor_is_the_target_user()
    {
        var actor = Guid.NewGuid();
        var target = actor;
        var tenant = Guid.NewGuid();
        var currentSession = Guid.NewGuid();
        var principal = McpTestHelpers.Principal(
            actor.ToString("D"),
            ["identity.mcp.user-access-summary.read"],
            tenant,
            superAdmin: true);
        principal.Identities.Single().AddClaim(new System.Security.Claims.Claim("sid", currentSession.ToString("D")));
        var resolver = CreateResolver(principal, McpTestHelpers.Membership(tenant));
        var tool = IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.GetUserAccessSummary];
        var input = McpTestHelpers.Input(new Dictionary<string, JsonElement>
        {
            ["userId"] = JsonSerializer.SerializeToElement(target)
        });

        var context = await resolver.ResolveAsync(input, tool, CancellationToken.None);

        Assert.Equal(currentSession, context.CurrentSessionId);
    }

    private static McpExecutionContextResolver CreateResolver(
        System.Security.Claims.ClaimsPrincipal principal,
        IReadOnlyCollection<IdentityService.Application.Features.UserTenants.Dtos.UserTenantDto>? memberships = null,
        McpTestHelpers.TestMediator? mediator = null)
    {
        var effectiveMediator = mediator ?? new McpTestHelpers.TestMediator(memberships ?? []);
        var options = McpTestHelpers.Options();
        return new McpExecutionContextResolver(
            new McpTestHelpers.StaticPrincipalResolver(principal),
            McpTestHelpers.EmptyHttpContextAccessor(),
            Options.Create(options),
            effectiveMediator,
            new McpSignedEnvelopeVerifier(Options.Create(options)),
            new InMemoryMcpNonceStore());
    }
}
