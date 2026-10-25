using System.Reflection;
using ModelContextProtocol.Server;
using {{App}}.Cli.Tests.Support;

namespace {{App}}.Cli.Tests;

/// <summary>The CLI and the MCP host are peers: every MCP tool has a CLI command of the same name and the other way round.</summary>
public class ParityTests
{
    [Fact]
    public void Cli_commands_and_mcp_tools_are_the_same_set()
    {
        var tools = Assembly.Load("{{App}}.Mcp").GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var commands = new CliHarness().Commands.Select(c => c.Tool).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.Equal(tools, commands);
    }
}
