using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace {{App}}.Mcp.Tests;

/// <summary>Hosts the real MCP server in-process with a known token and a throw-away database path.</summary>
public sealed class McpFactory : WebApplicationFactory<Program>
{
    public const string Token = "test-token-test-token-test-token";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DATABASE_CONNECTION_STRING", {{TestConnectionExpression}});
        builder.UseSetting("MCP_AUTH_TOKEN", Token);
    }
}
