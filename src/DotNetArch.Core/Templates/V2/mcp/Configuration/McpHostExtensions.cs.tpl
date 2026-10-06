using {{App}}.Mcp.Authentication;
using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace {{App}}.Mcp.Configuration;

public static class McpHostExtensions
{
    /// <summary>Registers the MCP server (stateless streamable HTTP), bearer-token authentication and every tool class in this assembly.</summary>
    public static IServiceCollection AddMcpHost(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<McpAuthOptions>()
            .Configure(options => options.Token = configuration[McpAuthOptions.TokenKey])
            .Validate(options => options.Token is { Length: >= McpAuthOptions.MinimumTokenLength },
                $"{McpAuthOptions.TokenKey} must be set to a secret of at least {McpAuthOptions.MinimumTokenLength} characters (environment or .env).")
            .ValidateOnStart();

        services.AddAuthentication(StaticTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, StaticTokenAuthenticationHandler>(StaticTokenAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = configuration["Mcp:ServerName"] ?? "{{ServerName}}",
                    Version = typeof(McpHostExtensions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
                };
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithToolsFromAssembly();

        return services;
    }

    public static WebApplication UseMcpHost(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/health", () => Results.Text("Healthy")).AllowAnonymous();
        app.MapMcp("/mcp").RequireAuthorization();
        return app;
    }
}
