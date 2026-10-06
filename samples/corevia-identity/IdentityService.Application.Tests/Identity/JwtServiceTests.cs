using System.IdentityModel.Tokens.Jwt;
using System.Text;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Services;
using IdentityService.Application.Tests.Support;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Application.Tests.Identity;

public sealed class JwtServiceTests
{
    private static readonly User Sample = new() { Id = Guid.NewGuid(), PhoneNumber = "09121234567", CreatedAt = DateTime.UtcNow };

    private static JwtService Service(JwtOptions? options = null) => new(Options.Create(options ?? Builders.Jwt()));

    [Fact]
    public void Access_token_is_signed_with_the_configured_secret_issuer_and_audience()
    {
        var token = Service().GenerateAccessToken(Sample, null, Array.Empty<string>(), null, null, out _);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Builders.Jwt().Issuer,
            ValidAudience = Builders.Jwt().Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Builders.JwtSecret)),
            ValidateLifetime = true
        };
        new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);
        Assert.Equal("HS256", ((JwtSecurityToken)validated).SignatureAlgorithm);
    }

    [Fact]
    public void Token_signed_with_another_secret_fails_validation()
    {
        var token = Service(new JwtOptions { Issuer = "i", Audience = "a", Secret = "a-different-signing-key-with-32-bytes-minimum!!" })
            .GenerateAccessToken(Sample, null, Array.Empty<string>(), null, null, out _);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Builders.JwtSecret))
        };
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _));
    }

    [Fact]
    public void Claims_include_subject_phone_jti_and_the_out_jti_matches()
    {
        var token = Service().GenerateAccessToken(Sample, null, Array.Empty<string>(), null, null, out var jti);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(Sample.Id.ToString(), jwt.Subject);
        Assert.Equal(Sample.PhoneNumber, jwt.Claims.Single(c => c.Type == "phone").Value);
        Assert.Equal(jti, jwt.Id);
        Assert.Equal(32, jti.Length);
    }

    [Fact]
    public void Optional_claims_are_only_present_when_supplied()
    {
        var bare = new JwtSecurityTokenHandler().ReadJwtToken(
            Service().GenerateAccessToken(Sample, null, Array.Empty<string>(), null, null, out _));
        Assert.DoesNotContain(bare.Claims, c => c.Type is "sid" or "client_id" or "tenant_id" or "scope" or "role");

        var session = Guid.NewGuid();
        var client = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var full = new JwtSecurityTokenHandler().ReadJwtToken(
            Service().GenerateAccessToken(Sample, session, new[] { "Customer" }, client, new[] { "a.read" }, out _, tenant));
        Assert.Equal(session.ToString(), full.Claims.Single(c => c.Type == "sid").Value);
        Assert.Equal(client.ToString(), full.Claims.Single(c => c.Type == "client_id").Value);
        Assert.Equal(tenant.ToString("D"), full.Claims.Single(c => c.Type == "tenant_id").Value);
    }

    [Fact]
    public void Roles_and_scopes_are_deduplicated_and_blank_entries_dropped_with_one_claim_per_value()
    {
        var token = Service().GenerateAccessToken(
            Sample, null, new[] { "Admin", "Admin", " ", "" }, null, new[] { "x.read", "x.read", "y.write", " " }, out _);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(new[] { "Admin" }, jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray());
        Assert.Equal(new[] { "x.read", "y.write" }, jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Lifetime_follows_AccessTokenMinutes()
    {
        var options = Builders.Jwt();
        options.AccessTokenMinutes = 5;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            Service(options).GenerateAccessToken(Sample, null, Array.Empty<string>(), null, null, out _));

        var lifetime = jwt.ValidTo - DateTime.UtcNow;
        Assert.InRange(lifetime.TotalMinutes, 4.5, 5.1);
    }

    [Fact]
    public void Refresh_tokens_are_random_256_bit_base64_values()
    {
        var service = Service();
        var tokens = Enumerable.Range(0, 20).Select(_ => service.GenerateRefreshToken()).ToArray();

        Assert.Equal(20, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.Equal(32, Convert.FromBase64String(t).Length));
    }
}
