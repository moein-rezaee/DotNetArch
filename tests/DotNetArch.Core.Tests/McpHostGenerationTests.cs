using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

public sealed class McpHostGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-mcphost-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public McpHostGenerationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IDisposable Host() => ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

    private string P(string relative) => Path.Combine(_root, "Acme", relative);

    private SolutionConfig Solution(bool mcp, string provider = "SQLite", bool noDocker = true)
    {
        using var _ = Host();
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", provider,
            Ops: new OpsOptions(Ci: "none", NoGit: true, NoDocker: noDocker, Mcp: mcp)));
        return ConfigManager.Load(P(""))!;
    }

    [Fact]
    public void The_mcp_host_references_application_and_infrastructure_only_and_is_authenticated()
    {
        var config = Solution(mcp: true);

        Assert.True(config.McpEnabled);
        var csproj = File.ReadAllText(P("src/Acme.Mcp/Acme.Mcp.csproj"));
        Assert.Contains("ModelContextProtocol.AspNetCore", csproj);
        Assert.Contains("Acme.Application", csproj);
        Assert.Contains("Acme.Infrastructure", csproj);
        Assert.DoesNotContain("Acme.Api", csproj);

        Assert.Contains("MapMcp(\"/mcp\").RequireAuthorization()", File.ReadAllText(P("src/Acme.Mcp/Configuration/McpHostExtensions.cs")));
        Assert.Contains("FixedTimeEquals", File.ReadAllText(P("src/Acme.Mcp/Authentication/StaticTokenAuthenticationHandler.cs")));
        Assert.Contains("MCP_AUTH_TOKEN=", File.ReadAllText(P("src/Acme.Mcp/.env.example")));
        Assert.DoesNotContain("MCP_AUTH_TOKEN", File.ReadAllText(P("src/Acme.Mcp/appsettings.json")));
        Assert.Matches("MCP_AUTH_TOKEN=[0-9a-f]{48}", File.ReadAllText(P("src/Acme.Mcp/.env")));
        Assert.True(File.Exists(P("tests/Acme.Mcp.Tests/McpEndpointTests.cs")));
        Assert.True(File.Exists(P("src/Acme.Mcp/Dockerfile")));
        Assert.Contains("Acme.Mcp.csproj", File.ReadAllText(P("src/Acme.Mcp/Dockerfile")));
    }

    [Fact]
    public void New_entities_and_actions_get_tools_that_send_the_same_requests_as_the_controllers()
    {
        var config = Solution(mcp: true);
        using var _ = Host();
        CrudScaffolder.Generate(config, "Product");
        ActionScaffolder.Generate(config, "Product", "Archive", "POST", crudStyle: false);
        ActionScaffolder.Generate(config, "Product", "Summary", "GET", crudStyle: false);

        var tools = File.ReadAllText(P("src/Acme.Mcp/Tools/Products/ProductsTools.cs"));
        Assert.Contains("Name = \"product_list\"", tools);
        Assert.Contains("Name = \"product_delete\", ReadOnly = false, Destructive = true", tools);
        Assert.Contains("new GetProductsQuery(pageNumber, pageSize)", tools);
        Assert.Contains("[McpServerToolType]", tools);
        Assert.Contains("Name = \"product_archive\", ReadOnly = false", File.ReadAllText(P("src/Acme.Mcp/Tools/Products/ProductsTools.Archive.cs")));
        Assert.Contains("Name = \"product_summary\", ReadOnly = true", File.ReadAllText(P("src/Acme.Mcp/Tools/Products/ProductsTools.Summary.cs")));
        Assert.True(File.Exists(P("tests/Acme.Mcp.Tests/Tools/ProductsToolsTests.cs")));
    }

    [Fact]
    public void Adding_the_host_later_backfills_tools_for_existing_entities_and_actions()
    {
        var config = Solution(mcp: false);
        using var _ = Host();
        CrudScaffolder.Generate(config, "Order");
        ActionScaffolder.Generate(config, "Order", "Ship", "POST", crudStyle: false);
        Assert.False(Directory.Exists(P("src/Acme.Mcp")));

        Assert.True(McpV2Generator.Add(config));

        Assert.True(File.Exists(P("src/Acme.Mcp/Tools/Orders/OrdersTools.cs")));
        Assert.True(File.Exists(P("src/Acme.Mcp/Tools/Orders/OrdersTools.Ship.cs")));
        Assert.True(ConfigManager.Load(P(""))!.McpEnabled);
        Assert.False(McpV2Generator.Add(config)); // already there
    }

    [Fact]
    public void Compose_override_for_the_mcp_host_exists_only_with_docker_and_matches_the_database()
    {
        Solution(mcp: true, provider: "Postgres", noDocker: false);

        var compose = File.ReadAllText(P("docker-compose.mcp.yml"));
        Assert.Contains("MCP_AUTH_TOKEN", compose);
        Assert.Contains("depends_on", compose);
        Assert.Contains("Host=db;Database=acme", compose);

        Directory.Delete(P(""), recursive: true);
        Solution(mcp: true, noDocker: true);
        Assert.False(File.Exists(P("docker-compose.mcp.yml")));
    }

    [Fact]
    public void A_solution_without_the_host_has_no_mcp_files()
    {
        Solution(mcp: false);

        Assert.False(Directory.Exists(P("src/Acme.Mcp")));
        Assert.False(ConfigManager.Load(P(""))!.McpEnabled);
    }
}
