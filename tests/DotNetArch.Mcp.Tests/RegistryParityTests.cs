using DotNetArch.Core.Operations;
using DotNetArch.Mcp.Tools;
using Xunit;

namespace DotNetArch.Mcp.Tests;

/// <summary>CLI and MCP derive from one registry (D-20): every operation is an MCP tool with the same name, description and nature.</summary>
public sealed class RegistryParityTests
{
    private static readonly IReadOnlyList<ModelContextProtocol.Server.McpServerTool> Tools = RegistryTools.Create().ToList();

    [Fact]
    public void Every_registry_operation_is_exposed_as_a_tool_with_the_same_name_and_description()
    {
        Assert.Equal(
            OperationRegistry.All.Select(o => o.Name).Order(StringComparer.Ordinal).ToArray(),
            Tools.Select(t => t.ProtocolTool.Name).Order(StringComparer.Ordinal).ToArray());
        foreach (var operation in OperationRegistry.All)
            Assert.Equal(operation.Description, Tools.Single(t => t.ProtocolTool.Name == operation.Name).ProtocolTool.Description);
    }

    [Fact]
    public void Only_read_only_operations_are_marked_read_only_and_none_is_destructive()
    {
        foreach (var operation in OperationRegistry.All)
        {
            var annotations = Tools.Single(t => t.ProtocolTool.Name == operation.Name).ProtocolTool.Annotations;
            Assert.Equal(operation.Kind == OperationKind.ReadOnly, annotations?.ReadOnlyHint == true);
            Assert.NotEqual(true, annotations?.DestructiveHint);
        }
    }

    [Fact]
    public void Mutating_tools_default_to_plan_only_by_declaring_an_apply_flag()
    {
        foreach (var operation in OperationRegistry.All)
        {
            var schema = Tools.Single(t => t.ProtocolTool.Name == operation.Name).ProtocolTool.InputSchema;
            Assert.Equal(operation.Kind == OperationKind.Mutating, schema.GetProperty("properties").TryGetProperty("apply", out _));
        }
    }
}
