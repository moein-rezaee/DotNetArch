using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>The CLI host is a peer of the MCP host: same requests, same names, generated for entities, actions and typed use cases (R-J3).</summary>
public sealed class CliHostGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-clihost-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public CliHostGenerationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IDisposable Host() => ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

    private string P(string relative) => Path.Combine(_root, "Acme", relative);

    private SolutionConfig Solution(bool mcp, bool cli)
    {
        using var _ = Host();
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite",
            Ops: new OpsOptions(Ci: "none", NoGit: true, NoDocker: true, Mcp: mcp, Cli: cli)));
        return ConfigManager.Load(P(""))!;
    }

    [Fact]
    public void The_cli_host_references_application_and_infrastructure_only_and_is_a_console_app()
    {
        var config = Solution(mcp: false, cli: true);

        Assert.True(config.CliEnabled);
        var csproj = File.ReadAllText(P("src/Acme.Cli/Acme.Cli.csproj"));
        Assert.Contains("<OutputType>Exe</OutputType>", csproj);
        Assert.Contains("<AssemblyName>acme</AssemblyName>", csproj);
        Assert.Contains("Acme.Application", csproj);
        Assert.Contains("Acme.Infrastructure", csproj);
        Assert.DoesNotContain("Acme.Api", csproj);
        Assert.DoesNotContain("Acme.Mcp", csproj);
        Assert.Contains("PackageVersion Include=\"Microsoft.Extensions.Hosting\"", File.ReadAllText(P("Directory.Packages.props")));
        Assert.Contains("DATABASE_CONNECTION_STRING=", File.ReadAllText(P("src/Acme.Cli/.env.example")));
        Assert.True(File.Exists(P("tests/Acme.Cli.Tests/CliBasicsTests.cs")));
        Assert.Contains("cli: true", File.ReadAllText(P("dotnet-arch.yml")));
    }

    [Fact]
    public void Entities_actions_and_use_cases_get_commands_when_the_host_exists_and_when_it_is_added_later()
    {
        var config = Solution(mcp: false, cli: true);
        CrudScaffolder.Generate(config, "Product");
        using (Host())
        {
            ActionScaffolder.Generate(config, "Product", "Archive", "POST", crudStyle: false);
            ActionScaffolder.Generate(config, "Product", "Activate", "POST", crudStyle: false, "Code:string,DeviceName:string?", "Token:string", true, null);
        }

        Assert.Contains("class ProductListCliCommand", File.ReadAllText(P("src/Acme.Cli/Commands/Products/ProductsCommands.cs")));
        Assert.Contains("Name => \"archive\"", File.ReadAllText(P("src/Acme.Cli/Commands/Products/ArchiveProductCliCommand.cs")));
        var typed = File.ReadAllText(P("src/Acme.Cli/Commands/Products/ActivateProductCliCommand.cs"));
        Assert.Contains("context.Require<string>(\"code\")", typed);
        Assert.Contains("context.Optional<string?>(\"device-name\")", typed);
        Assert.Contains("new ActivateProductCommand(Code: code, DeviceName: deviceName)", typed);
        Assert.True(File.Exists(P("tests/Acme.Cli.Tests/Commands/ProductsCommandsTests.cs")));
    }

    [Fact]
    public void A_parity_test_exists_only_when_both_peers_exist_whichever_is_added_second()
    {
        var cliFirst = Solution(mcp: false, cli: true);
        Assert.False(File.Exists(P("tests/Acme.Cli.Tests/ParityTests.cs")));

        using (Host())
            McpV2Generator.Add(cliFirst);

        var parity = File.ReadAllText(P("tests/Acme.Cli.Tests/ParityTests.cs"));
        Assert.Contains("Assembly.Load(\"Acme.Mcp\")", parity);
        Assert.Contains(_runner.Calls, c => c.Arguments.Contains("reference") && c.Arguments.Contains("src/Acme.Mcp/Acme.Mcp.csproj"));
    }

    [Fact]
    public void The_cli_layer_is_recognised_by_the_doctor_state()
    {
        Solution(mcp: true, cli: true);

        Assert.True(File.Exists(P("tests/Acme.Cli.Tests/ParityTests.cs")));
    }
}
