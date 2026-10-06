using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;

namespace IdentityService.Mcp.Mcp;

public enum McpTransportKind
{
    Http,
    Stdio
}

/// <summary>
/// Selects the MCP transport. HTTP is the default; stdio is a development-only process transport and is
/// rejected outside the Development environment (Identity MCP standard profile).
/// </summary>
public static class McpTransportSelector
{
    public static McpTransportKind Resolve(string? configuredTransport, string? environmentName)
    {
        var value = string.IsNullOrWhiteSpace(configuredTransport) ? "http" : configuredTransport.Trim();

        if (value.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return McpTransportKind.Http;
        }

        if (value.Equals("stdio", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return McpTransportKind.Stdio;
            }

            throw new BadRequestException(
                "The stdio MCP transport is development-only; use the http transport outside the Development environment.",
                "mcp_stdio_dev_only");
        }

        throw new BadRequestException(
            $"Unsupported MCP transport '{value}'. Supported values: http (default), stdio (development only).",
            "mcp_transport_unsupported");
    }
}
