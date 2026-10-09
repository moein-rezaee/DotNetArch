using DotNetArch.Core.Hosting;
using DotNetArch.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace DotNetArch.Mcp;

/// <summary>Runs <c>dotnet-arch mcp serve</c>: an MCP server over stdio. Stdout belongs to the protocol, so all logging goes to stderr.</summary>
public static class McpServerHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "dotnet-arch",
                    Title = "DotNetArch",
                    Version = typeof(McpServerHost).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithTools<DotNetArchTools>()
            .WithTools(RegistryTools.Create());

        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
