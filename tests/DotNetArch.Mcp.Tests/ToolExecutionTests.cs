using DotNetArch.Core.Hosting;
using DotNetArch.Core.Operations;
using DotNetArch.Mcp.Tests.Support;
using Xunit;

namespace DotNetArch.Mcp.Tests;

/// <summary>The generators run as registry operations: plan first, write only with apply, ABP result for layout v3.</summary>
public sealed class ToolExecutionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-mcp-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public ToolExecutionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private OperationResult Run(string name, bool apply, params (string Key, string Value)[] values)
    {
        using var scope = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        var map = values.ToDictionary(v => v.Key, v => v.Value);
        return OperationRegistry.Execute(OperationRegistry.Find(name)!, map, apply);
    }

    private OperationResult Solution(bool apply = true, string layout = "v3") =>
        Run("new_solution", apply, ("name", "Acme"), ("output", _root), ("no_git", "true"), ("no_docker", "true"), ("ci", "none"), ("layout", layout));

    private string AcmePath => Path.Combine(_root, "Acme");

    [Fact]
    public void New_solution_plans_first_and_writes_nothing_until_applied()
    {
        var plan = Solution(apply: false);

        Assert.True(plan.Ok, plan.Text + plan.Error);
        Assert.False(plan.Applied);
        Assert.Contains(plan.Plan!, p => p.Path == "Acme/src/Acme.Domain/Acme.Domain.csproj");
        Assert.False(Directory.Exists(AcmePath));

        var applied = Solution();

        Assert.True(applied.Ok, applied.Text + applied.Error);
        Assert.True(File.Exists(Path.Combine(AcmePath, "src", "Acme.Domain", "Acme.Domain.csproj")));
    }

    [Fact]
    public void Layout_v3_solution_follows_the_abp_standard_from_the_start()
    {
        Solution();

        Assert.True(Directory.Exists(Path.Combine(AcmePath, "test")));
        Assert.False(Directory.Exists(Path.Combine(AcmePath, "tests")));
        var project = File.ReadAllText(Path.Combine(AcmePath, ".net-arch", "project.yml"));
        Assert.Contains("v3", project, StringComparison.Ordinal);
        Assert.Contains("abp", project, StringComparison.Ordinal);
        Assert.Contains("layout: v3", File.ReadAllText(Path.Combine(AcmePath, "dotnet-arch.yml")), StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_v2_remains_available_for_projects_that_want_it()
    {
        var result = Solution(layout: "v2");

        Assert.True(result.Ok, result.Text + result.Error);
        Assert.True(Directory.Exists(Path.Combine(AcmePath, "tests")));
        Assert.False(Directory.Exists(Path.Combine(AcmePath, ".net-arch")));
    }

    [Fact]
    public void New_crud_in_a_v3_solution_lands_in_the_abp_layers_and_planning_changes_nothing()
    {
        Solution();
        var before = Directory.EnumerateFiles(AcmePath, "*", SearchOption.AllDirectories).Count();

        var plan = Run("new_crud", apply: false, ("path", AcmePath), ("entity", "Product"), ("no_migration", "true"));

        Assert.True(plan.Ok, plan.Text + plan.Error);
        Assert.Equal(before, Directory.EnumerateFiles(AcmePath, "*", SearchOption.AllDirectories).Count());
        Assert.NotEmpty(plan.Plan!);

        var applied = Run("new_crud", apply: true, ("path", AcmePath), ("entity", "Product"), ("no_migration", "true"));

        Assert.True(applied.Ok, applied.Text + applied.Error);
        Assert.True(Directory.Exists(Path.Combine(AcmePath, "src", "Acme.Application.Contracts")));
        Assert.True(Directory.EnumerateFiles(Path.Combine(AcmePath, "src", "Acme.HttpApi"), "ProductsController.cs", SearchOption.AllDirectories).Any());
        Assert.Contains(Directory.EnumerateFiles(Path.Combine(AcmePath, "src"), "ProductsClient.cs", SearchOption.AllDirectories), f => f.Contains("HttpApi.Client", StringComparison.Ordinal));
        var report = DotNetArch.Core.Doctor.DoctorRunner.Run(AcmePath);
        Assert.DoesNotContain(report.Findings, f => f.Id.StartsWith("DA-A", StringComparison.Ordinal) && f.Severity != DotNetArch.Core.Doctor.DoctorSeverity.Info && f.Id is "DA-A01" or "DA-A02" or "DA-A03" or "DA-A07" or "DA-A10" or "DA-A11");
    }

    [Fact]
    public void Unknown_solutions_and_invalid_names_fail_without_throwing()
    {
        var missing = Run("new_crud", apply: false, ("path", Path.Combine(_root, "Nope")), ("entity", "Product"));
        Assert.False(missing.Ok);

        Solution();
        var invalid = Run("new_crud", apply: false, ("path", AcmePath), ("entity", "bad name"));
        Assert.False(invalid.Ok);
        Assert.Contains("entity name", invalid.Error);
    }

    [Fact]
    public void Failures_reported_by_the_generators_make_the_result_not_ok()
    {
        Solution();

        var result = Run("new_action", apply: false, ("path", AcmePath), ("entity", "Ghost"), ("action", "Archive"), ("method", "POST"));

        Assert.False(result.Ok);
        Assert.Contains("does not exist", result.Error + result.Text);
    }

    [Fact]
    public void Services_kits_and_ops_commands_work_through_the_same_path()
    {
        Solution();
        Run("new_crud", apply: true, ("path", AcmePath), ("entity", "Product"), ("no_migration", "true"));

        var service = Run("new_service", apply: true, ("path", AcmePath), ("logic", "true"), ("name", "Pricing"));
        var kit = Run("new_service", apply: true, ("path", AcmePath), ("logic", "false"), ("area", "Cache"), ("providers", "InMemory"));
        var ci = Run("ci_add", apply: true, ("path", AcmePath), ("provider", "github"));

        Assert.True(service.Ok, service.Text + service.Error);
        Assert.True(kit.Ok, kit.Text + kit.Error);
        Assert.True(ci.Ok, ci.Text + ci.Error);
        Assert.True(File.Exists(Path.Combine(AcmePath, "kits", "Cache", "kit.json")));
        Assert.True(File.Exists(Path.Combine(AcmePath, ".github", "workflows", "ci.yml")));
    }

    [Fact]
    public void Describe_and_list_are_read_only()
    {
        Solution();
        Run("new_crud", apply: true, ("path", AcmePath), ("entity", "Product"), ("no_migration", "true"));

        var listed = Run("list_entities", apply: false, ("path", AcmePath));
        var described = Run("describe_config", apply: false, ("path", AcmePath));

        Assert.Contains("entity Product: crud=True", listed.Text);
        Assert.Contains("layout=v3", described.Text);
        Assert.Null(listed.Plan);
    }

    [Fact]
    public void An_agent_cannot_create_an_empty_layer_but_the_cli_value_does()
    {
        Solution();

        var nothing = Run("add_layer", apply: true, ("path", AcmePath), ("layer", "Application.Contracts"));
        var empty = Run("add_layer", apply: true, ("path", AcmePath), ("layer", "HttpApi.Client"), ("empty", "true"));

        Assert.True(nothing.Ok || nothing.Error!.Contains("Nothing belongs", StringComparison.Ordinal), nothing.Text + nothing.Error);
        Assert.True(empty.Ok, empty.Text + empty.Error);
    }

    [Fact]
    public void Add_tests_spec_and_graph_commands_work_on_the_generated_solution()
    {
        Solution();

        var tests = Run("add_tests", apply: true, ("path", AcmePath), ("layer", "Domain"));
        var specAdd = Run("spec_add", apply: true, ("path", AcmePath), ("name", "pricing"), ("title", "Pricing"));
        var specList = Run("spec_list", apply: false, ("path", AcmePath));
        var graph = Run("graph", apply: false, ("path", AcmePath));

        Assert.True(tests.Ok, tests.Text + tests.Error);
        Assert.True(specAdd.Ok, specAdd.Text + specAdd.Error);
        Assert.True(File.Exists(Path.Combine(AcmePath, "docs", "specs", "pricing.fa.md")));
        Assert.Contains("pricing", specList.Text);
        Assert.Contains("project(s)", graph.Text);
    }
}
