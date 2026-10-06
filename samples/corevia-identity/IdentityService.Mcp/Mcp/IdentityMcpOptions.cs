namespace IdentityService.Mcp.Mcp;

public sealed class IdentityMcpOptions
{
    public const string SectionName = "Mcp";

    public string ServerName { get; set; } = "identity-service-mcp";
    public string ServerVersion { get; set; } = "0.1.0";
    public string Audience { get; set; } = "identity-service-mcp";
    public string Transport { get; set; } = "stdio";
    public string HttpBindAddress { get; set; } = "127.0.0.1";
    public int HttpPort { get; set; } = 5271;
    public bool HttpHttpsEnabled { get; set; }
    public bool PrivateNetworkTrusted { get; set; }
    public string[] AllowedHosts { get; set; } = ["127.0.0.1", "[::1]", "localhost"];
    public string[] AllowedOrigins { get; set; } = [];

    // These values are runtime secrets. They must be supplied by Vault/environment and are
    // intentionally absent from appsettings.json and documentation examples.
    public string? DelegationSigningKey { get; set; }
    public string? ApprovalSigningKey { get; set; }
}
