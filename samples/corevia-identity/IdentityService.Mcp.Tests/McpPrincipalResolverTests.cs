using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Mcp.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Mcp.Tests;

public sealed class McpPrincipalResolverTests
{
    [Fact]
    public void Stdio_token_is_validated_and_http_never_falls_back_to_process_credential()
    {
        var actor = Guid.NewGuid().ToString("D");
        var token = CreateToken(actor);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["COREVIA_MCP_BEARER_TOKEN"] = token,
                ["MCP_TEST_JWT_SECRET"] = McpTestHelpers.JwtSecret
            })
            .Build();
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "IdentityService",
            Audience = "IdentityClients",
            Secret = configuration["MCP_TEST_JWT_SECRET"]!
        });
        var httpAccessor = new HttpContextAccessor();
        var resolver = new DefaultMcpPrincipalResolver(httpAccessor, configuration, jwtOptions);

        var stdioPrincipal = resolver.Resolve();
        Assert.NotNull(stdioPrincipal);
        Assert.Equal(actor, stdioPrincipal!.FindFirstValue("sub"));

        httpAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        Assert.Null(resolver.Resolve());
    }

    [Fact]
    public void Invalid_stdio_token_is_rejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["COREVIA_MCP_BEARER_TOKEN"] = "not-a-jwt",
                ["MCP_TEST_JWT_SECRET"] = McpTestHelpers.JwtSecret
            })
            .Build();
        var resolver = new DefaultMcpPrincipalResolver(
            new HttpContextAccessor(),
            configuration,
            Options.Create(new JwtOptions
            {
                Issuer = "IdentityService",
                Audience = "IdentityClients",
                Secret = configuration["MCP_TEST_JWT_SECRET"]!
            }));

        Assert.Null(resolver.Resolve());
    }

    private static string CreateToken(string actor)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(McpTestHelpers.JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "IdentityService",
            audience: "IdentityClients",
            claims:
            [
                new Claim("sub", actor),
                new Claim("scope", "identity.mcp.self.read")
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
