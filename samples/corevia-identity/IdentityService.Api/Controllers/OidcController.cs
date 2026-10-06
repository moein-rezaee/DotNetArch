using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Features.Identity.Models;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Commands.Refresh;
using IdentityService.Application.Features.Identity.Commands.Logout;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Api.Controllers;

[ApiController]
public class OidcController : ControllerBase
{
    private readonly JwtOptions _jwtOptions;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IdentityClientOptions _identityClientOptions;
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtBearerOptions;

    public OidcController(
        IOptions<JwtOptions> jwtOptions,
        IOptions<IdentityClientOptions> identityClientOptions,
        IOptionsMonitor<JwtBearerOptions> jwtBearerOptions,
        IMediator mediator,
        IUnitOfWork unitOfWork)
    {
        _jwtOptions = jwtOptions.Value;
        _identityClientOptions = identityClientOptions.Value;
        _jwtBearerOptions = jwtBearerOptions;
        _mediator = mediator;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [AllowAnonymous]
    [Route(".well-known/openid-configuration")]
    public IActionResult GetOpenIdConfiguration()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var config = new
        {
            issuer = _jwtOptions.Issuer,
            authorization_endpoint = $"{baseUrl}/authorize",
            token_endpoint = $"{baseUrl}/token",
            revocation_endpoint = $"{baseUrl}/revoke",
            introspection_endpoint = $"{baseUrl}/introspect",
            userinfo_endpoint = $"{baseUrl}/userinfo",
            jwks_uri = $"{baseUrl}/.well-known/jwks.json",
            response_types_supported = new[] { "code", "token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.HmacSha256 },
            token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" },
            grant_types_supported = new[] { "refresh_token", "client_credentials" }
        };
        return Ok(config);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route(".well-known/jwks.json")]
    public IActionResult GetJwks()
    {
        // Symmetric key is not exposed; introspection should be used instead.
        var jwks = new { keys = Array.Empty<object>() };
        return Ok(jwks);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("authorize")]
    public IActionResult AuthorizeEndpoint()
    {
        return BadRequest(new { error = "unsupported_response_type" });
    }

    public sealed record TokenRequest(string grant_type, string? refresh_token, string? client_id, string? client_secret);

    [HttpPost]
    [AllowAnonymous]
    [Route("token")]
    public async Task<IActionResult> TokenEndpoint([FromForm] TokenRequest request, CancellationToken ct)
    {
        if (string.Equals(request.grant_type, "refresh_token", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(request.refresh_token))
            {
                return BadRequest(new { error = "invalid_request" });
            }

            var result = await _mediator.Send(new RefreshCommand(new RefreshRequest(request.refresh_token)), ct);
            var expiresIn = _jwtOptions.AccessTokenMinutes * 60;
            return Ok(new
            {
                access_token = result.AccessToken,
                refresh_token = result.RefreshToken,
                token_type = "Bearer",
                expires_in = expiresIn
            });
        }

        if (string.Equals(request.grant_type, "client_credentials", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(request.client_id) || string.IsNullOrWhiteSpace(request.client_secret))
            {
                return BadRequest(new { error = "invalid_request", error_description = "client_id and client_secret are required" });
            }

            // Find client
            var clientRepo = _unitOfWork.Repository<Client>();
            var client = await clientRepo.FirstOrDefaultAsync(c => c.ClientId == request.client_id && c.IsActive, ct);
            if (client is null)
            {
                return BadRequest(new { error = "invalid_client", error_description = "Client not found or inactive" });
            }

            // Validate secret
            var secretRepo = _unitOfWork.Repository<ClientSecret>();
            var clientSecrets = await secretRepo.Query()
                .Where(s => s.ClientId == client.Id && s.RevokedAt == null)
                .ToListAsync(ct);

            var requestSecretHash = HashSecret(request.client_secret);
            var validSecret = clientSecrets.FirstOrDefault(s =>
                (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow) &&
                s.Hash == requestSecretHash);

            if (validSecret is null)
            {
                return BadRequest(new { error = "invalid_client", error_description = "Invalid client secret" });
            }

            // Keep this as a correlated EXISTS query. Materializing scope IDs and
            // using Enumerable.Contains makes EF Core emit OPENJSON on SQL Server,
            // which is unavailable on the legacy compatibility level used by
            // customer Sepidar databases.
            var clientScopeRepo = _unitOfWork.Repository<ClientScope>();
            var scopeRepo = _unitOfWork.Repository<Scope>();
            var scopes = await scopeRepo.Query()
                .Where(s => clientScopeRepo.Query()
                    .Any(cs => cs.ClientId == client.Id && cs.ScopeId == s.Id))
                .ToListAsync(ct);
            var scopeNames = scopes.Select(s => s.Name).ToList();

            // Generate access token for service
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim("sub", client.Id.ToString()),
                new Claim("client_id", client.ClientId),
                new Claim("client_name", client.Name),
                new Claim("token_type", "service")
            };

            if (_identityClientOptions.M2MClients?.TryGetValue(request.client_id, out var configuredClient) == true &&
                configuredClient.TenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", configuredClient.TenantId.Value.ToString("D")));
            }

            if (scopeNames.Count > 0)
            {
                // Add each scope as a separate claim for proper Ocelot authorization
                foreach (var scope in scopeNames)
                {
                    claims.Add(new Claim("scope", scope));
                }
            }

            var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: accessTokenExpiry,
                signingCredentials: credentials);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var expiresIn = _jwtOptions.AccessTokenMinutes * 60;

            return Ok(new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = expiresIn,
                scope = string.Join(' ', scopeNames)
            });
        }

        return BadRequest(new { error = "unsupported_grant_type" });
    }

    public sealed record RevokeRequest(string token, string? token_type_hint);

    [HttpPost]
    [AllowAnonymous]
    [Route("revoke")]
    public async Task<IActionResult> RevokeEndpoint([FromForm] RevokeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.token))
        {
            return BadRequest(new { error = "invalid_request" });
        }

        await _mediator.Send(new LogoutCommand(new LogoutRequest(request.token)), ct);
        return Ok();
    }

    public sealed record IntrospectRequest(string token, string? token_type_hint, string? client_id, string? client_secret);

    [HttpPost]
    [AllowAnonymous]
    [Route("introspect")]
    public async Task<IActionResult> IntrospectEndpoint([FromForm] IntrospectRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.token))
        {
            return Ok(new { active = false });
        }

        if (!string.IsNullOrWhiteSpace(request.client_id) && !string.IsNullOrWhiteSpace(request.client_secret))
        {
            var clientRepo = _unitOfWork.Repository<Client>();
            var secretRepo = _unitOfWork.Repository<ClientSecret>();
            var client = clientRepo.Query().FirstOrDefault(c => c.ClientId == request.client_id && c.IsActive);
            if (client is null)
            {
                return Ok(new { active = false });
            }

            var hashed = HashSecret(request.client_secret);
            var hasActiveSecret = secretRepo.Query()
                .Any(s => s.ClientId == client.Id && s.RevokedAt == null && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow) && s.Hash == hashed);
            if (!hasActiveSecret)
            {
                return Ok(new { active = false });
            }
        }

        // Try refresh token first
        var rtRepo = _unitOfWork.Repository<RefreshToken>();
        var rt = await rtRepo.FirstOrDefaultAsync(x => x.Token == request.token, ct);
        if (rt != null)
        {
            var active = rt.RevokedAt is null && (!rt.ExpiresAt.HasValue || rt.ExpiresAt > DateTime.UtcNow);
            return Ok(new
            {
                active,
                sub = rt.UserId.ToString(),
                exp = rt.ExpiresAt.HasValue
                    ? new DateTimeOffset(rt.ExpiresAt.Value).ToUnixTimeSeconds()
                    : (long?)null,
                token_type = "refresh_token",
                client_id = rt.ClientId?.ToString()
            });
        }

        // Try access token (JWT)
        // Validation parameters come from the Corevia.Kit.JwtSecurity bearer registration
        // (AddJwtSecurityExtension), so introspection validates exactly what the API authenticates.
        var parameters = _jwtBearerOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(request.token, parameters, out var validatedToken);
            var jwt = (JwtSecurityToken)validatedToken;
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue("sub") ?? string.Empty;
            var exp = jwt.ValidTo;

            return Ok(new
            {
                active = true,
                sub,
                exp = new DateTimeOffset(exp).ToUnixTimeSeconds(),
                token_type = "access_token",
                client_id = principal.FindFirstValue("client_id"),
                scope = string.Join(' ', principal.FindAll("scope").Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)))
            });
        }
        catch
        {
            return Ok(new { active = false });
        }
    }

    [HttpGet]
    [Authorize]
    [Route("userinfo")]
    public IActionResult UserInfoEndpoint()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue("sub") ?? string.Empty;
        var phone = User.FindFirstValue("phone");
        var sid = User.FindFirstValue("sid");

        var result = new Dictionary<string, object?>
        {
            ["sub"] = sub,
            ["phone"] = phone,
            ["sid"] = sid
        };

        return Ok(result);
    }

    private static string HashSecret(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
