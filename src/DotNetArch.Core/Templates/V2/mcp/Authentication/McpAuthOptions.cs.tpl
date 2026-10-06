namespace {{App}}.Mcp.Authentication;

public sealed class McpAuthOptions
{
    /// <summary>Secret key (environment / .env, UPPER_CASE). Never put the token in appsettings.</summary>
    public const string TokenKey = "MCP_AUTH_TOKEN";

    public const int MinimumTokenLength = 24;

    public string? Token { get; set; }
}
