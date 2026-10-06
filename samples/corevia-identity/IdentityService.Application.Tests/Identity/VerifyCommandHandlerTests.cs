using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Commands.Verify;
using IdentityService.Application.Features.Identity.Models;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Tests.Support;
using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Tests.Identity;

public sealed class VerifyCommandHandlerTests : IDisposable
{
    private const string Phone = "09121234567";

    private readonly TestDb _db = TestDb.Create();
    private readonly FakeOtpClient _otp = new();

    public void Dispose() => _db.Dispose();

    private VerifyCommandHandler Handler(RootAdminOptions? root = null, IdentityClientOptions? clients = null)
        => new(
            _otp,
            _db.Uow,
            Builders.JwtService(),
            Options.Create(Builders.Jwt()),
            Options.Create(root ?? new RootAdminOptions()),
            Options.Create(clients ?? new IdentityClientOptions()));

    private static VerifyCommand Command(string phone = Phone)
        => new(new VerifyCodeRequest(phone, "12345"), "10.0.0.7", "xunit-agent");

    private static JwtSecurityToken Read(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

    [Fact]
    public async Task Invalid_code_is_unauthorized_with_invalid_verification_code_and_creates_nothing()
    {
        _otp.VerifyResult = false;

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().Handle(Command(), default));

        Assert.Equal("invalid_verification_code", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Empty(_db.NewContext().Users);
        Assert.Empty(_db.NewContext().RefreshTokens);
    }

    [Fact]
    public async Task First_login_creates_user_session_and_non_expiring_customer_refresh_token()
    {
        var (user, access, refresh) = await Handler().Handle(Command(), default);

        using var verify = _db.NewContext();
        var stored = await verify.Users.SingleAsync();
        Assert.Equal(Phone, stored.PhoneNumber);
        Assert.Equal(user.Id, stored.Id);

        var session = await verify.UserSessions.SingleAsync();
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal("10.0.0.7", session.IpAddress);
        Assert.Equal("xunit-agent", session.DeviceInfo);
        Assert.False(session.IsRevoked);

        var token = await verify.RefreshTokens.SingleAsync();
        Assert.Equal(refresh, token.Token);
        Assert.Null(token.ExpiresAt); // customer refresh tokens never expire
        Assert.Null(token.RevokedAt);
        Assert.Equal(session.Id, token.UserSessionId);

        var jwt = Read(access);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(Phone, jwt.Claims.Single(c => c.Type == "phone").Value);
        Assert.Equal(session.Id.ToString(), jwt.Claims.Single(c => c.Type == "sid").Value);
        Assert.Equal(token.JwtId, jwt.Id);
    }

    [Fact]
    public async Task Existing_user_is_reused_not_duplicated()
    {
        var existing = await Builders.AddUserAsync(_db.Uow, Phone);

        var (user, _, _) = await Handler().Handle(Command(), default);

        Assert.Equal(existing.Id, user.Id);
        Assert.Equal(1, await _db.NewContext().Users.CountAsync());
    }

    [Fact]
    public async Task Each_login_creates_an_independent_session_and_refresh_token()
    {
        var handler = Handler();
        var first = await handler.Handle(Command(), default);
        var second = await handler.Handle(Command(), default);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        using var verify = _db.NewContext();
        Assert.Equal(2, await verify.UserSessions.CountAsync());
        Assert.Equal(2, await verify.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Root_admin_phone_gets_superadmin_and_customer_roles_and_every_scope_in_the_token()
    {
        await Builders.AddScopeAsync(_db.Uow, "alpha.read");
        await Builders.AddScopeAsync(_db.Uow, "beta.write");

        var (_, access, _) = await Handler(new RootAdminOptions { PhoneNumber = Phone }).Handle(Command(), default);

        using var verify = _db.NewContext();
        var roleNames = await verify.Roles.Select(r => r.Name).ToListAsync();
        Assert.Contains("SuperAdmin", roleNames);
        Assert.Contains("Customer", roleNames);

        var jwt = Read(access);
        Assert.Contains(jwt.Claims, c => c is { Type: "role", Value: "SuperAdmin" });
        var scopes = jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "alpha.read", "beta.write" }, scopes);
    }

    [Fact]
    public async Task Non_root_phone_does_not_get_privileged_roles()
    {
        var (_, access, _) = await Handler(new RootAdminOptions { PhoneNumber = "09999999999" }).Handle(Command(), default);

        Assert.Empty(await _db.NewContext().Roles.ToListAsync());
        Assert.DoesNotContain(Read(access).Claims, c => c.Type == "role");
    }

    [Fact]
    public async Task Default_public_client_scopes_are_embedded_and_client_recorded_on_the_refresh_token()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "web-public");
        var scope = await Builders.AddScopeAsync(_db.Uow, "catalog.read");
        await Builders.AddScopeAsync(_db.Uow, "unrelated.scope");
        await Builders.LinkClientScopeAsync(_db.Uow, client.Id, scope.Id);

        var (_, access, _) = await Handler(clients: new IdentityClientOptions { DefaultPublicClientId = "web-public" })
            .Handle(Command(), default);

        var jwt = Read(access);
        Assert.Equal(new[] { "catalog.read" }, jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToArray());
        Assert.Equal(client.Id.ToString(), jwt.Claims.Single(c => c.Type == "client_id").Value);
        Assert.Equal(client.Id, (await _db.NewContext().RefreshTokens.SingleAsync()).ClientId);
    }

    [Fact]
    public async Task Unknown_default_public_client_is_unauthorized_invalid_client()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Handler(clients: new IdentityClientOptions { DefaultPublicClientId = "missing" }).Handle(Command(), default));

        Assert.Equal("invalid_client", ex.ErrorCode);
    }

    [Fact]
    public async Task Inactive_default_public_client_is_unauthorized_invalid_client()
    {
        await Builders.AddClientAsync(_db.Uow, "web-public", active: false);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Handler(clients: new IdentityClientOptions { DefaultPublicClientId = "web-public" }).Handle(Command(), default));

        Assert.Equal("invalid_client", ex.ErrorCode);
    }

    [Fact]
    public async Task Single_tenant_membership_is_put_in_the_tenant_id_claim()
    {
        var user = await Builders.AddUserAsync(_db.Uow, Phone);
        var tenant = await Builders.AddTenantAsync(_db.Uow, "acme");
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, tenant.Id);

        var (_, access, _) = await Handler().Handle(Command(), default);

        Assert.Equal(tenant.Id.ToString("D"), Read(access).Claims.Single(c => c.Type == "tenant_id").Value);
    }

    [Fact]
    public async Task Multiple_memberships_use_the_single_default_tenant()
    {
        var user = await Builders.AddUserAsync(_db.Uow, Phone);
        var a = await Builders.AddTenantAsync(_db.Uow, "a");
        var b = await Builders.AddTenantAsync(_db.Uow, "b");
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, a.Id);
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, b.Id, isDefault: true);

        var (_, access, _) = await Handler().Handle(Command(), default);

        Assert.Equal(b.Id.ToString("D"), Read(access).Claims.Single(c => c.Type == "tenant_id").Value);
    }

    [Fact]
    public async Task Ambiguous_memberships_without_a_single_default_omit_the_tenant_claim()
    {
        var user = await Builders.AddUserAsync(_db.Uow, Phone);
        var a = await Builders.AddTenantAsync(_db.Uow, "a");
        var b = await Builders.AddTenantAsync(_db.Uow, "b");
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, a.Id);
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, b.Id);

        var (_, access, _) = await Handler().Handle(Command(), default);

        Assert.DoesNotContain(Read(access).Claims, c => c.Type == "tenant_id");
    }

    [Fact]
    public async Task Otp_dependency_failure_propagates_as_external_service_exception_and_creates_nothing()
    {
        _otp.ThrowOnVerify = new ExternalServiceException("OTP service is unavailable.", HttpStatusCode.BadGateway);

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Handler().Handle(Command(), default));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Empty(await _db.NewContext().Users.ToListAsync());
    }
}
