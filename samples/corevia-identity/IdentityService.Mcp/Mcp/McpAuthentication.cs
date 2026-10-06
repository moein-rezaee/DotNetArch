using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Features.Identity.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Mcp.Mcp;

public interface IMcpPrincipalResolver
{
    ClaimsPrincipal? Resolve();
}

public sealed class DefaultMcpPrincipalResolver(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IOptions<JwtOptions> jwtOptions) : IMcpPrincipalResolver
{
    public ClaimsPrincipal? Resolve()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            // Never fall back to the stdio process credential for an HTTP request.
            // Otherwise an accidentally exposed HTTP listener could inherit the local
            // launcher identity and bypass the HTTP authentication boundary.
            return httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User
                : null;
        }

        // Stdio has no ASP.NET authentication middleware. The trusted local launcher
        // supplies a short-lived JWT through the process environment. The token is
        // validated here; its claims are never accepted from tool arguments or prompt text.
        var token = Environment.GetEnvironmentVariable("COREVIA_MCP_BEARER_TOKEN")
                    ?? configuration["COREVIA_MCP_BEARER_TOKEN"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(jwtOptions.Value.Secret))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Value.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Value.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            return handler.ValidateToken(token, validation, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
