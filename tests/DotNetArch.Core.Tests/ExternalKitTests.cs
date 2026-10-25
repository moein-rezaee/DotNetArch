using System.Text.Json.Nodes;
using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.NetArch;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>A profile lists published kits; a new solution wires them into its hosts without the tool knowing any kit (R-J4).</summary>
public sealed class ExternalKitTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-extkits-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public ExternalKitTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string P(string relative) => Path.Combine(_root, "Acme", relative);

    private static ProfileDefinition Profile() => new()
    {
        Name = "org",
        Version = "1",
        ExternalKits =
        {
            new ExternalKit
            {
                Area = "Loader",
                Packages = { new ExternalKitPackage { Id = "Org.Kit.Loader", Version = "1.2.3", Projects = { "api", "mcp", "cli" } } },
                Hosts =
                {
                    ["api"] = new KitHostWiring { Usings = { "Org.Kit.Loader" }, Bootstrap = { "LoaderBootstrap.Load();" }, Builder = { "builder.AddLoader();" }, App = { "app.UseLoader();" } },
                    ["cli"] = new KitHostWiring { Usings = { "Org.Kit.Loader" }, Builder = { "builder.AddLoader();" } },
                },
                Settings = { ["Loader:Mode"] = "Central", ["Loader:Retries"] = "3" },
                Secrets = { "LOADER_TOKEN_KEY" },
                TestSettings = { ["LOADER_TOKEN_KEY"] = "test-value" },
            },
            new ExternalKit
            {
                Area = "Docs",
                Packages = { new ExternalKitPackage { Id = "Org.Kit.Docs", Version = "2.0.0", Projects = { "api" } } },
                Hosts = { ["api"] = new KitHostWiring { Builder = { "builder.Services.AddDocs<{{App}}.Domain.Common.DomainException>();" } } },
                Replaces = { "swagger", "exception-handler" },
            },
        },
    };

    private SolutionConfig Solution()
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite",
            Ops: new OpsOptions(Ci: "none", NoGit: true, NoDocker: true, Mcp: true, Cli: true)));
        var config = ConfigManager.Load(P(""))!;
        using var apply = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        ExternalKitWiring.Apply(config, Profile());
        return config;
    }

    [Fact]
    public void Lines_go_to_the_hosts_that_use_the_kit_at_the_markers()
    {
        Solution();

        var api = File.ReadAllText(P("src/Acme.Api/Program.cs"));
        Assert.Contains("using Org.Kit.Loader;", api);
        Assert.True(api.IndexOf("LoaderBootstrap.Load();", StringComparison.Ordinal) < api.IndexOf("WebApplication.CreateBuilder", StringComparison.Ordinal));
        Assert.True(api.IndexOf("builder.AddLoader();", StringComparison.Ordinal) > api.IndexOf("AddAppConfiguration", StringComparison.Ordinal));
        Assert.True(api.IndexOf("app.UseLoader();", StringComparison.Ordinal) > api.IndexOf("builder.Build()", StringComparison.Ordinal));
        Assert.Contains("AddDocs<Acme.Domain.Common.DomainException>", api);
        Assert.Contains("builder.AddLoader();", File.ReadAllText(P("src/Acme.Cli/Program.cs")));
        Assert.DoesNotContain("AddLoader", File.ReadAllText(P("src/Acme.Mcp/Program.cs")));
    }

    [Fact]
    public void Packages_get_a_central_version_and_a_reference_in_the_listed_projects_only()
    {
        Solution();

        var props = File.ReadAllText(P("Directory.Packages.props"));
        Assert.Contains("<PackageVersion Include=\"Org.Kit.Loader\" Version=\"1.2.3\" />", props);
        Assert.Contains("<PackageVersion Include=\"Org.Kit.Docs\" Version=\"2.0.0\" />", props);
        Assert.Contains("<PackageReference Include=\"Org.Kit.Loader\" />", File.ReadAllText(P("src/Acme.Api/Acme.Api.csproj")));
        Assert.Contains("<PackageReference Include=\"Org.Kit.Loader\" />", File.ReadAllText(P("src/Acme.Mcp/Acme.Mcp.csproj")));
        Assert.DoesNotContain("Org.Kit.Docs", File.ReadAllText(P("src/Acme.Mcp/Acme.Mcp.csproj")));
    }

    [Fact]
    public void Settings_secrets_and_test_values_are_written_where_the_kit_is_used_and_never_overwrite_existing_ones()
    {
        Solution();

        var settings = JsonNode.Parse(File.ReadAllText(P("src/Acme.Api/appsettings.json")))!;
        Assert.Equal("Central", settings["Loader"]!["Mode"]!.GetValue<string>());
        Assert.Equal(3, settings["Loader"]!["Retries"]!.GetValue<int>());
        Assert.Contains("\"Loader\"", File.ReadAllText(P("src/Acme.Api/appsettings.example.json")));
        Assert.DoesNotContain("Loader", File.ReadAllText(P("src/Acme.Mcp/appsettings.json")));
        Assert.Contains("LOADER_TOKEN_KEY=", File.ReadAllText(P("src/Acme.Api/.env.example")));
        Assert.Contains("\"LOADER_TOKEN_KEY\",", File.ReadAllText(P("src/Acme.Api/Configuration/ConfigurationContract.cs")));
        Assert.Contains("UseSetting(\"LOADER_TOKEN_KEY\", \"test-value\");", File.ReadAllText(P("tests/Acme.Api.Tests/Support/ApiFactory.cs")));
    }

    [Fact]
    public void Replaced_parts_are_removed_and_a_second_run_changes_nothing()
    {
        var config = Solution();

        var extensions = File.ReadAllText(P("src/Acme.Api/Configuration/ApiServiceCollectionExtensions.cs"));
        Assert.DoesNotContain("AddSwaggerGen", extensions);
        Assert.DoesNotContain("UseSwagger", extensions);
        Assert.DoesNotContain("AddExceptionHandler", extensions);
        Assert.DoesNotContain("UseExceptionHandler", extensions);
        Assert.False(File.Exists(P("src/Acme.Api/Middleware/GlobalExceptionHandler.cs")));
        Assert.DoesNotContain("Swashbuckle", File.ReadAllText(P("src/Acme.Api/Acme.Api.csproj")));

        var before = File.ReadAllText(P("src/Acme.Api/Program.cs")) + File.ReadAllText(P("Directory.Packages.props")) + File.ReadAllText(P("src/Acme.Api/.env.example"));
        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner)))
            ExternalKitWiring.Apply(config, Profile());
        var after = File.ReadAllText(P("src/Acme.Api/Program.cs")) + File.ReadAllText(P("Directory.Packages.props")) + File.ReadAllText(P("src/Acme.Api/.env.example"));
        Assert.Equal(before, after);
    }

    [Fact]
    public void Unknown_hosts_and_replaced_parts_in_a_profile_are_rejected()
    {
        var config = Solution();
        var bad = new ProfileDefinition { ExternalKits = { new ExternalKit { Area = "X", Hosts = { ["web"] = new KitHostWiring() } } } };
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

        Assert.Throws<ArgumentException>(() => ExternalKitWiring.Apply(config, bad));
        Assert.Throws<ArgumentException>(() => ExternalKitWiring.Apply(config, new ProfileDefinition { ExternalKits = { new ExternalKit { Area = "Y", Replaces = { "everything" } } } }));
    }
}
