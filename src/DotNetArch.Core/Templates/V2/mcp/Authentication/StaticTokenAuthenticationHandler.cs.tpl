using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace {{App}}.Mcp.Authentication;

/// <summary>Bearer-token authentication against the configured <c>MCP_AUTH_TOKEN</c>, compared in constant time.</summary>
internal sealed class StaticTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<McpAuthOptions> auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "McpToken";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var provided = SHA256.HashData(Encoding.UTF8.GetBytes(header["Bearer ".Length..].Trim()));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(auth.Value.Token ?? string.Empty));
        if (!CryptographicOperations.FixedTimeEquals(provided, expected))
            return Task.FromResult(AuthenticateResult.Fail("Invalid token."));

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "mcp-client") }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
