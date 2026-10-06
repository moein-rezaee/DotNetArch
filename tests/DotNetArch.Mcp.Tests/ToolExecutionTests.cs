using DotNetArch.Mcp.Tests.Support;
using DotNetArch.Mcp.Tools;
using Xunit;

namespace DotNetArch.Mcp.Tests;

public sealed class ToolExecutionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-mcp-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();
    private readonly DotNetArchTools _tools;

    public ToolExecutionTests()
    {
        Directory.CreateDirectory(_root);
        _tools = new DotNetArchTools(_runner);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private Task<ToolResult> Solution() =>
        _tools.NewSolution("Acme", _root, noGit: true, noDocker: true, ci: "none");

    [Fact]
    public async Task New_solution_reports_created_files_and_the_equivalent_command()
    {
        var result = await Solution();

        Assert.True(result.Ok, result.Output + result.Error);
        Assert.StartsWith("dotnet-arch new solution Acme", result.Command);
        Assert.Contains("src/Acme.Domain/Acme.Domain.csproj", result.Created);
        Assert.Contains("dotnet-arch.yml", result.Created);
        Assert.Empty(result.Modified);
    }

    [Fact]
    public async Task New_crud_reports_the_slice_and_can_skip_the_migration()
    {
        await Solution();
        var before = _runner.Calls.Count;

        var result = await _tools.NewCrud(Path.Combine(_root, "Acme"), "Product", skipMigration: true);

        Assert.True(result.Ok, result.Output + result.Error);
        Assert.Contains("src/Acme.Api/Controllers/Products/ProductsController.cs", result.Created);
        Assert.Contains("dotnet-arch.yml", result.Modified);
        Assert.DoesNotContain(_runner.Calls.Skip(before), call => call.Arguments.Contains("ef"));
    }

    [Fact]
    public async Task Unknown_solutions_and_invalid_names_fail_without_throwing()
    {
        var missing = await _tools.NewCrud(Path.Combine(_root, "Nope"), "Product");
        Assert.False(missing.Ok);
        Assert.Contains("dotnet-arch.yml", missing.Error);

        await Solution();
        var invalid = await _tools.NewCrud(Path.Combine(_root, "Acme"), "bad name", skipMigration: true);
        Assert.False(invalid.Ok);
        Assert.Contains("entity name", invalid.Error);
    }

    [Fact]
    public async Task Failures_reported_by_the_generators_make_the_result_not_ok()
    {
        await Solution();

        var result = await _tools.NewAction(Path.Combine(_root, "Acme"), "Ghost", "Archive", "POST");

        Assert.False(result.Ok);
        Assert.Contains("does not exist", result.Output);
    }

    [Fact]
    public async Task Services_kits_and_ops_tools_work_through_the_same_path()
    {
        await Solution();
        var path = Path.Combine(_root, "Acme");
        await _tools.NewCrud(path, "Product", skipMigration: true);

        var service = await _tools.NewService(path, hasBusinessLogic: true, name: "Pricing");
        var kit = await _tools.NewService(path, hasBusinessLogic: false, area: "Cache", providers: new[] { "InMemory" });
        var ci = await _tools.CiAdd(path, "github");
        var docker = await _tools.DockerAdd(path);

        Assert.True(service.Ok, service.Output + service.Error);
        Assert.True(kit.Ok, kit.Output + kit.Error);
        Assert.True(ci.Ok, ci.Output + ci.Error);
        Assert.True(docker.Ok, docker.Output + docker.Error);
        Assert.Contains("src/Acme.Application/Common/Services/Pricing.cs", service.Created);
        Assert.Contains("kits/Cache/kit.json", kit.Created);
        Assert.Contains(".github/workflows/ci.yml", ci.Created);
        Assert.Contains("docker-compose.yml", docker.Created);
    }

    [Fact]
    public async Task Describe_and_list_are_read_only()
    {
        await Solution();
        var path = Path.Combine(_root, "Acme");
        await _tools.NewCrud(path, "Product", skipMigration: true);

        var listed = await _tools.ListEntities(path);
        var described = await _tools.DescribeConfig(path);

        Assert.Contains("entity Product: crud=True", listed.Output);
        Assert.Contains("layout=v2", described.Output);
        Assert.Empty(listed.Created);
        Assert.Empty(described.Modified);
    }
}
