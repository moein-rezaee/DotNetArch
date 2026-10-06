using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Api.Controllers;
using IdentityService.Api.Tests.Support;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Tests;

public sealed class OidcControllerTests : IDisposable
{
    private const string ClientPublicId = "svc-invoice";

    private readonly ApiFixture _api;

    public OidcControllerTests()
    {
        _api = new ApiFixture(new IdentityClientOptions());
    }

    public void Dispose() => _api.Dispose();

    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static T Field<T>(object? payload, string name)
    {
        var property = payload!.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException($"No property {name} on {payload.GetType().Name}");
        return (T)property.GetValue(payload)!;
    }

    private static object? Value(IActionResult result) => result switch
    {
        OkObjectResult ok => ok.Value,
        BadRequestObjectResult bad => bad.Value,
        _ => null
    };

    private async Task<(Client Client, string Secret)> SeedClientAsync(
        string? secretOverride = null, DateTime? expires = null, DateTime? revoked = null, bool active = true, params string[] scopes)
    {
        var secret = secretOverride ?? ("s-" + Guid.NewGuid().ToString("N"));
        var client = await Builders.AddClientAsync(_api.Uow, ClientPublicId, active);
        await _api.Uow.Repository<ClientSecret>().AddAsync(new ClientSecret
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Hash = Hash(secret),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expires,
            RevokedAt = revoked
        });
        foreach (var name in scopes)
        {
            var scope = await Builders.AddScopeAsync(_api.Uow, name);
            await Builders.LinkClientScopeAsync(_api.Uow, client.Id, scope.Id);
        }

        await _api.Uow.SaveChangesAsync();
        return (client, secret);
    }

    // ------------------------------------------------------------ client_credentials
    [Fact]
    public async Task Client_credentials_issues_a_service_token_with_the_client_scopes_and_expiry()
    {
        var (client, secret) = await SeedClientAsync(scopes: ["invoice.read", "invoice.write"]);

        var result = await _api.Oidc().TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, ClientPublicId, secret), default);

        var payload = Assert.IsType<OkObjectResult>(result).Value;
        Assert.Equal("Bearer", Field<string>(payload, "token_type"));
        Assert.Equal(600, Field<int>(payload, "expires_in"));
        Assert.Equal(new[] { "invoice.read", "invoice.write" }, Field<string>(payload, "scope").Split(' ').OrderBy(x => x).ToArray());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Field<string>(payload, "access_token"));
        Assert.Equal(client.Id.ToString(), jwt.Subject);
        Assert.Equal(ClientPublicId, jwt.Claims.Single(c => c.Type == "client_id").Value);
        Assert.Equal("service", jwt.Claims.Single(c => c.Type == "token_type").Value);
        Assert.Equal(new[] { "invoice.read", "invoice.write" }, jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).OrderBy(x => x).ToArray());
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "tenant_id");
        Assert.Equal(_api.Jwt.Issuer, jwt.Issuer);
    }

    [Fact]
    public async Task Client_credentials_adds_the_tenant_claim_only_from_the_explicit_M2M_tenant_binding()
    {
        var tenant = Guid.NewGuid();
        using var api = new ApiFixture(new IdentityClientOptions
        {
            M2MClients = new() { [ClientPublicId] = new M2MClientConfig { TenantId = tenant } }
        });
        var client = await Builders.AddClientAsync(api.Uow, ClientPublicId);
        var secret = "s-" + Guid.NewGuid().ToString("N");
        await api.Uow.Repository<ClientSecret>().AddAsync(new ClientSecret { Id = Guid.NewGuid(), ClientId = client.Id, Hash = Hash(secret), CreatedAt = DateTime.UtcNow });
        await api.Uow.SaveChangesAsync();

        var result = await api.Oidc().TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, ClientPublicId, secret), default);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Field<string>(Value(result), "access_token"));
        Assert.Equal(tenant.ToString("D"), jwt.Claims.Single(c => c.Type == "tenant_id").Value);
    }

    [Fact]
    public async Task Client_credentials_rejects_missing_wrong_revoked_expired_and_inactive_credentials_with_invalid_client()
    {
        var (_, secret) = await SeedClientAsync(scopes: ["invoice.read"]);
        var oidc = _api.Oidc();

        async Task<IActionResult> Call(string? id, string? s)
            => await oidc.TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, id, s), default);

        var missing = Assert.IsType<BadRequestObjectResult>(await Call(null, secret));
        Assert.Equal("invalid_request", Field<string>(missing.Value, "error"));
        Assert.Equal("invalid_request", Field<string>(Value(await Call(ClientPublicId, " ")), "error"));

        var unknownClient = await Call("nobody", secret);
        Assert.Equal("invalid_client", Field<string>(Value(unknownClient), "error"));
        var wrongSecret = await Call(ClientPublicId, "wrong");
        Assert.Equal("invalid_client", Field<string>(Value(wrongSecret), "error"));
        Assert.Equal("Invalid client secret", Field<string>(Value(wrongSecret), "error_description"));
    }

    [Fact]
    public async Task Revoked_secret_expired_secret_and_inactive_client_are_all_refused()
    {
        var (client, revokedSecret) = await SeedClientAsync(revoked: DateTime.UtcNow.AddMinutes(-1));
        var expiredSecret = "e-" + Guid.NewGuid().ToString("N");
        await _api.Uow.Repository<ClientSecret>().AddAsync(new ClientSecret
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Hash = Hash(expiredSecret),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _api.Uow.SaveChangesAsync();
        var oidc = _api.Oidc();

        foreach (var s in new[] { revokedSecret, expiredSecret })
        {
            var result = await oidc.TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, ClientPublicId, s), default);
            Assert.Equal("invalid_client", Field<string>(Value(result), "error"));
        }

        client.IsActive = false;
        await _api.Uow.SaveChangesAsync();
        var inactive = await oidc.TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, ClientPublicId, revokedSecret), default);
        Assert.Equal("Client not found or inactive", Field<string>(Value(inactive), "error_description"));
    }

    // ------------------------------------------------------------ refresh_token + other grants
    [Fact]
    public async Task Refresh_token_grant_rotates_the_token_and_returns_oauth_fields()
    {
        var identity = _api.Identity();
        var login = (Application.Features.Identity.Models.TokenResponse)((OkObjectResult)(await identity.Verify(
            new Application.Features.Identity.Models.VerifyCodeRequest("09121234567", "12345"), default)).Result!).Value!;

        var result = await _api.Oidc().TokenEndpoint(new OidcController.TokenRequest("refresh_token", login.RefreshToken, null, null), default);

        var payload = Assert.IsType<OkObjectResult>(result).Value;
        Assert.NotEqual(login.RefreshToken, Field<string>(payload, "refresh_token"));
        Assert.Equal("Bearer", Field<string>(payload, "token_type"));
        Assert.Equal(600, Field<int>(payload, "expires_in"));
    }

    [Fact]
    public async Task Refresh_token_grant_requires_a_token_and_propagates_invalid_refresh_token()
    {
        var oidc = _api.Oidc();

        var missing = await oidc.TokenEndpoint(new OidcController.TokenRequest("refresh_token", null, null, null), default);
        Assert.Equal("invalid_request", Field<string>(Value(missing), "error"));
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            oidc.TokenEndpoint(new OidcController.TokenRequest("refresh_token", "unknown", null, null), default));
        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
    }

    [Fact]
    public async Task Unsupported_grant_types_and_the_authorize_endpoint_are_rejected()
    {
        var oidc = _api.Oidc();

        var password = await oidc.TokenEndpoint(new OidcController.TokenRequest("password", null, null, null), default);
        Assert.Equal("unsupported_grant_type", Field<string>(Value(password), "error"));
        var authorize = oidc.AuthorizeEndpoint();
        Assert.Equal("unsupported_response_type", Field<string>(Value(authorize), "error"));
    }

    // ------------------------------------------------------------ revoke
    [Fact]
    public async Task Revoke_requires_a_token_and_revokes_a_refresh_token()
    {
        var login = (Application.Features.Identity.Models.TokenResponse)((OkObjectResult)(await _api.Identity().Verify(
            new Application.Features.Identity.Models.VerifyCodeRequest("09121234567", "12345"), default)).Result!).Value!;
        var oidc = _api.Oidc();

        Assert.IsType<BadRequestObjectResult>(await oidc.RevokeEndpoint(new OidcController.RevokeRequest("", null), default));
        Assert.IsType<OkResult>(await oidc.RevokeEndpoint(new OidcController.RevokeRequest(login.RefreshToken, "refresh_token"), default));

        var introspected = await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(login.RefreshToken, null, null, null), default);
        Assert.False(Field<bool>(Value(introspected), "active"));
    }

    // ------------------------------------------------------------ introspect
    [Fact]
    public async Task Introspect_empty_token_is_inactive()
    {
        var result = await _api.Oidc().IntrospectEndpoint(new OidcController.IntrospectRequest("", null, null, null), default);

        Assert.False(Field<bool>(Value(result), "active"));
    }

    [Fact]
    public async Task Introspect_customer_refresh_token_is_active_without_an_exp()
    {
        var login = (Application.Features.Identity.Models.TokenResponse)((OkObjectResult)(await _api.Identity().Verify(
            new Application.Features.Identity.Models.VerifyCodeRequest("09121234567", "12345"), default)).Result!).Value!;

        var result = await _api.Oidc().IntrospectEndpoint(new OidcController.IntrospectRequest(login.RefreshToken, null, null, null), default);

        var payload = Value(result);
        Assert.True(Field<bool>(payload, "active"));
        Assert.Equal("refresh_token", Field<string>(payload, "token_type"));
        Assert.Null(payload!.GetType().GetProperty("exp")!.GetValue(payload));
    }

    [Fact]
    public async Task Introspect_expired_legacy_refresh_token_is_inactive_and_future_one_is_active_with_exp()
    {
        var user = await Builders.AddUserAsync(_api.Uow);
        var expiredToken = Guid.NewGuid().ToString("N");
        var futureToken = Guid.NewGuid().ToString("N");
        await _api.Uow.Repository<RefreshToken>().AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = expiredToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _api.Uow.Repository<RefreshToken>().AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = futureToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await _api.Uow.SaveChangesAsync();
        var oidc = _api.Oidc();

        var expired = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(expiredToken, null, null, null), default));
        var future = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(futureToken, null, null, null), default));

        Assert.False(Field<bool>(expired, "active"));
        Assert.True(Field<bool>(future, "active"));
        Assert.NotNull(future!.GetType().GetProperty("exp")!.GetValue(future));
    }

    [Fact]
    public async Task Introspect_valid_access_token_is_active_with_sub_client_and_scopes_and_garbage_is_inactive()
    {
        var (_, secret) = await SeedClientAsync(scopes: ["invoice.read"]);
        var oidc = _api.Oidc();
        var issued = Value(await oidc.TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, ClientPublicId, secret), default));
        var accessToken = Field<string>(issued, "access_token");

        var active = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(accessToken, "access_token", null, null), default));
        var garbage = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest("not.a.jwt", null, null, null), default));

        Assert.True(Field<bool>(active, "active"));
        Assert.Equal("access_token", Field<string>(active, "token_type"));
        Assert.Equal(ClientPublicId, Field<string>(active, "client_id"));
        Assert.Equal("invoice.read", Field<string>(active, "scope"));
        Assert.False(Field<bool>(garbage, "active"));
    }

    [Fact]
    public async Task Introspect_access_token_failing_the_API_validation_parameters_is_inactive()
    {
        var (_, secret) = await SeedClientAsync();
        var issued = Value(await _api.Oidc().TokenEndpoint(new OidcController.TokenRequest("client_credentials", null, ClientPublicId, secret), default));
        var wrongAudience = _api.Oidc(new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = _api.Jwt.Issuer,
            ValidAudience = "some-other-audience",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFixture.Secret))
        });

        var result = Value(await wrongAudience.IntrospectEndpoint(
            new OidcController.IntrospectRequest(Field<string>(issued, "access_token"), null, null, null), default));

        Assert.False(Field<bool>(result, "active"));
    }

    [Fact]
    public async Task Introspect_with_client_authentication_requires_a_valid_active_client_secret()
    {
        var (_, secret) = await SeedClientAsync();
        var login = (Application.Features.Identity.Models.TokenResponse)((OkObjectResult)(await _api.Identity().Verify(
            new Application.Features.Identity.Models.VerifyCodeRequest("09121234567", "12345"), default)).Result!).Value!;
        var oidc = _api.Oidc();

        var good = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(login.RefreshToken, null, ClientPublicId, secret), default));
        var badSecret = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(login.RefreshToken, null, ClientPublicId, "wrong"), default));
        var badClient = Value(await oidc.IntrospectEndpoint(new OidcController.IntrospectRequest(login.RefreshToken, null, "nobody", secret), default));

        Assert.True(Field<bool>(good, "active"));
        Assert.False(Field<bool>(badSecret, "active"));
        Assert.False(Field<bool>(badClient, "active"));
    }
}
