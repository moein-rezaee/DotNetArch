using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Application.Features.Identity.Services;

public interface IJwtService
{
    string GenerateAccessToken(
        User user,
        Guid? sessionId,
        IEnumerable<string> roles,
        Guid? clientId,
        IEnumerable<string>? scopes,
        out string jti,
        Guid? tenantId = null);
    string GenerateRefreshToken();
}

public class JwtService(IOptions<JwtOptions> options) : IJwtService
{
    private readonly JwtOptions _opts = options.Value;

    public string GenerateAccessToken(
        User user,
        Guid? sessionId,
        IEnumerable<string> roles,
        Guid? clientId,
        IEnumerable<string>? scopes,
        out string jti,
        Guid? tenantId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("phone", user.PhoneNumber),
            new(JwtRegisteredClaimNames.Jti, jti)
        };
        if (sessionId.HasValue)
        {
            claims.Add(new Claim("sid", sessionId.Value.ToString()));
        }

        var roleList = roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList()
                       ?? new List<string>();
        foreach (var role in roleList)
        {
            claims.Add(new Claim("role", role));
        }

        if (clientId.HasValue)
        {
            claims.Add(new Claim("client_id", clientId.Value.ToString()));
        }

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString("D")));
        }

        var scopeList = scopes?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (scopeList is { Count: > 0 })
        {
            // Add each scope as a separate claim for proper Ocelot authorization
            foreach (var scope in scopeList)
            {
                claims.Add(new Claim("scope", scope));
            }
        }
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
