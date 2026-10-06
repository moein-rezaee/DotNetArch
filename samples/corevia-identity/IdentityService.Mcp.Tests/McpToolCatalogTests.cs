using IdentityService.Mcp.Mcp;
using ModelContextProtocol.Server;
using System.Reflection;

namespace IdentityService.Mcp.Tests;

public sealed class McpToolCatalogTests
{
    [Fact]
    public void Catalog_contains_curated_atomic_and_business_tools_with_explicit_policy()
    {
        Assert.Equal(41, IdentityMcpToolCatalog.All.Count);
        Assert.Equal(36, IdentityMcpToolCatalog.Active.Count);
        Assert.Equal(5, IdentityMcpToolCatalog.All.Values.Count(tool => tool.Status == "deprecated" && !tool.Exposable));
        Assert.Contains(IdentityMcpToolCatalog.Active.Values, tool => tool.Kind == "atomic");
        Assert.Contains(IdentityMcpToolCatalog.Active.Values, tool => tool.Kind == "business");

        foreach (var tool in IdentityMcpToolCatalog.Active.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.Version));
            Assert.False(string.IsNullOrWhiteSpace(tool.ApiPermission));
            Assert.Contains(tool.Audience, new[] { "self", "admin" });
            Assert.Contains(tool.Risk, new[] { "read", "write", "sensitive" });
            Assert.NotEmpty(tool.RequiredScopes);
            Assert.Contains(tool.McpPermission, tool.RequiredScopes);
            Assert.False(tool.InputSchema["additionalProperties"]?.GetValue<bool>() ?? true);
        }

        var sensitive = IdentityMcpToolCatalog.Active[IdentityMcpToolCatalog.RevokeUserSessions];
        Assert.True(sensitive.ApprovalRequired);
        Assert.Contains("tenantId", sensitive.ForbiddenInputs);
        Assert.DoesNotContain("call_identity_api", IdentityMcpToolCatalog.Active.Keys);
    }

    [Fact]
    public void Every_active_catalog_tool_is_registered_with_the_mcp_server()
    {
        var registeredTools = new[]
            {
                typeof(IdentityMcpTools),
                typeof(IdentityMcpAdminTools)
            }
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var activeCatalogTools = IdentityMcpToolCatalog.Active.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(activeCatalogTools, registeredTools);
    }

    [Fact]
    public void Protocol_operations_are_inventory_only_and_never_exposable()
    {
        var protocolTools = IdentityMcpToolCatalog.All.Values
            .Where(tool => tool.ApiPermission?.StartsWith("Identity.Token.", StringComparison.Ordinal) == true
                || tool.ApiPermission?.StartsWith("Identity.ClientSecrets.", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(5, protocolTools.Length);
        Assert.All(protocolTools, tool =>
        {
            Assert.Equal("deprecated", tool.Status);
            Assert.False(tool.Exposable);
            Assert.DoesNotContain(tool.Name, IdentityMcpToolCatalog.Active.Keys);
        });
    }
}
