using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using DotNetArch.Mcp.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace DotNetArch.Mcp.Tests;

public class ToolCatalogTests
{
    private static readonly (MethodInfo Method, McpServerToolAttribute Tool)[] Tools = typeof(DotNetArchTools)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute>()!))
        .Where(entry => entry.Tool is not null)
        .ToArray();

    [Fact]
    public void The_documented_tools_exist() =>
        Assert.Equal(
            new[]
            {
                "add_kit", "add_mcp", "ci_add", "describe_config", "docker_add", "doctor", "git_setup", "list_entities", "new_action", "new_constant",
                "new_crud", "new_enum", "new_event", "new_kit", "new_service", "new_solution"
            },
            Tools.Select(entry => entry.Tool.Name!).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void Names_are_unique_snake_case() =>
        Assert.All(Tools, entry => Assert.Matches("^[a-z]+(_[a-z]+)*$", entry.Tool.Name!));

    [Fact]
    public void Every_tool_and_parameter_that_needs_explaining_has_a_description()
    {
        Assert.All(Tools, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Method.GetCustomAttribute<DescriptionAttribute>()?.Description), entry.Tool.Name));
    }

    [Fact]
    public void Only_the_read_only_tools_are_marked_read_only() =>
        Assert.Equal(
            new[] { "describe_config", "doctor", "list_entities" },
            Tools.Where(entry => entry.Tool.ReadOnly).Select(entry => entry.Tool.Name!).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void No_tool_deletes_anything() =>
        Assert.DoesNotContain(Tools, entry => Regex.IsMatch(entry.Tool.Name!, "delete|remove|drop", RegexOptions.IgnoreCase));
}
