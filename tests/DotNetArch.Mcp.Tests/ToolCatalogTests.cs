using System.Text.RegularExpressions;
using DotNetArch.Core.Operations;
using DotNetArch.Mcp.Tools;
using Xunit;

namespace DotNetArch.Mcp.Tests;

/// <summary>The command catalogue (CLI list == MCP tools == registry) and its rules.</summary>
public class ToolCatalogTests
{
    private static readonly string[] Documented =
    {
        "add_kit", "add_layer", "add_mcp", "add_tests", "adopt", "ci_add", "describe_config", "docker_add", "doctor", "exec", "fix", "git_setup", "graph",
        "list_entities", "new_action", "new_constant", "new_crud", "new_enum", "new_event", "new_kit", "new_service", "new_solution", "remove_migration",
        "spec_add", "spec_check", "spec_list",
    };

    [Fact]
    public void The_documented_commands_exist_in_the_registry_and_as_tools() =>
        Assert.Equal(Documented, OperationRegistry.All.Select(o => o.Name).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void Names_are_unique_snake_case() =>
        Assert.All(OperationRegistry.All, o => Assert.Matches("^[a-z]+(_[a-z]+)*$", o.Name));

    [Fact]
    public void Every_command_and_parameter_has_a_description() =>
        Assert.All(OperationRegistry.All, o =>
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Description), o.Name);
            Assert.All(o.Parameters, p => Assert.False(string.IsNullOrWhiteSpace(p.Description), $"{o.Name}.{p.Name}"));
        });

    [Fact]
    public void Only_the_inspecting_commands_are_read_only() =>
        Assert.Equal(
            new[] { "describe_config", "doctor", "graph", "list_entities", "spec_check", "spec_list" },
            OperationRegistry.All.Where(o => o.Kind == OperationKind.ReadOnly).Select(o => o.Name).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void Commands_that_remove_or_run_something_are_mutating_and_therefore_plan_first() =>
        Assert.All(OperationRegistry.All.Where(o => Regex.IsMatch(o.Name, "remove|exec")), o => Assert.Equal(OperationKind.Mutating, o.Kind));

    [Fact]
    public void Cli_only_parameters_are_not_offered_to_agents()
    {
        var tool = RegistryTools.Create().Single(t => t.ProtocolTool.Name == "add_layer");
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("layer", out _));
        Assert.False(properties.TryGetProperty("empty", out _));
    }
}
