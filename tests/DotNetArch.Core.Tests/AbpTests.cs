using DotNetArch.Core.Doctor;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Operations;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>ABP alignment: layout v3 (tests/ to test/), the ABP layer projects and the built-in opt-in <c>abp</c> rule set (AC-19..AC-21).</summary>
public sealed class AbpTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-abp-" + Guid.NewGuid().ToString("N"));

    public AbpTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Repo(string name = "shop")
    {
        var repo = Path.Combine(_root, name);
        Write(repo, "Shop.sln", "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Api\", \"src\\Shop.Api\\Shop.Api.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Api.Tests\", \"tests\\Shop.Api.Tests\\Shop.Api.Tests.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\nEndProject\n");
        Write(repo, "src/Shop.Domain/Shop.Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>\n</Project>\n");
        Write(repo, "src/Shop.Application/Shop.Application.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Domain\\Shop.Domain.csproj\" /></ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.Application/Features/Products/ProductDto.cs", "namespace Shop.Application.Features.Products;\npublic sealed record ProductDto(int Id);\n");
        Write(repo, "src/Shop.Domain/Products/IProductRepository.cs", "namespace Shop.Domain.Products;\npublic interface IProductRepository\n{\n    Task<Product?> GetAsync(int id, CancellationToken cancellationToken = default);\n    IQueryable<Product> Query();\n    Product Find(int id);\n    Task AddAsync(Product product);\n}\npublic sealed class Product { }\npublic sealed class PricingService { }\n");
        Write(repo, "src/Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Application\\Shop.Application.csproj\" /></ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.Api/Dockerfile", "COPY src/Shop.Api/Shop.Api.csproj src/Shop.Api/\nCOPY tests/Shop.Api.Tests/Shop.Api.Tests.csproj tests/Shop.Api.Tests/\n");
        Write(repo, "tests/Shop.Api.Tests/Shop.Api.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\..\\src\\Shop.Api\\Shop.Api.csproj\" /></ItemGroup>\n</Project>\n");
        Write(repo, "tests/Shop.Api.Tests/PathTests.cs", "class PathTests\n{\n    string A(string root) => Path.Combine(root, \"tests\", \"Shop.Api.Tests\");\n    string B = \"tests/Shop.Api.Tests/x\";\n    string C = \"docs/tests/keep\";\n}\n");
        Write(repo, "docker-compose.yml", "services:\n  api:\n    volumes:\n      - ./tests/Shop.Api.Tests:/t\n");
        Write(repo, ".gitlab-ci.yml", "test:\n  script:\n    - dotnet test tests/Shop.Api.Tests\n");
        Write(repo, "docs/evidence/old.md", "ran tests/Shop.Api.Tests\n");
        Write(repo, "docs/specs/contracts.md", "Tests live in tests/Shop.Api.Tests and docs/tests/.\n");
        Write(repo, ".net-arch/project.yml", "schema: 2\nblueprint: 1.0.0\nlayout: v2\n");
        return repo;
    }

    private static void Write(string repo, string relative, string content)
    {
        var path = Path.Combine(repo, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static OperationResult Run(string name, string repo, bool apply, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string> { ["path"] = repo };
        foreach (var (key, value) in extra)
            values[key] = value;
        return OperationRegistry.Execute(OperationRegistry.Find(name)!, values, apply);
    }

    [Fact]
    public void Layout_v3_move_is_opt_in_renames_tests_and_rewrites_every_path_that_points_at_it()
    {
        var repo = Repo();
        Assert.Equal("v2", DoctorRunner.Run(repo).Layout);

        var listed = Run("fix", repo, apply: false);
        Assert.DoesNotContain(listed.Plan!, c => c.RuleId == "DA-A01");

        var plan = Run("fix", repo, apply: false, ("rules", "DA-A01"));
        Assert.Contains(plan.Plan!, c => c.Action == "move" && c.Path == "test");
        Assert.True(Directory.Exists(Path.Combine(repo, "tests")));

        Run("fix", repo, apply: true, ("rules", "DA-A01"));

        Assert.False(Directory.Exists(Path.Combine(repo, "tests")));
        Assert.True(File.Exists(Path.Combine(repo, "test", "Shop.Api.Tests", "Shop.Api.Tests.csproj")));
        Assert.Contains("\"test\\Shop.Api.Tests\\Shop.Api.Tests.csproj\"", File.ReadAllText(Path.Combine(repo, "Shop.sln")), StringComparison.Ordinal);
        Assert.Contains("Include=\"..\\..\\src\\Shop.Api\\Shop.Api.csproj\"", File.ReadAllText(Path.Combine(repo, "test", "Shop.Api.Tests", "Shop.Api.Tests.csproj")), StringComparison.Ordinal);
        Assert.Contains("COPY test/Shop.Api.Tests/Shop.Api.Tests.csproj test/Shop.Api.Tests/", File.ReadAllText(Path.Combine(repo, "src", "Shop.Api", "Dockerfile")), StringComparison.Ordinal);
        Assert.Contains("./test/Shop.Api.Tests:/t", File.ReadAllText(Path.Combine(repo, "docker-compose.yml")), StringComparison.Ordinal);
        Assert.Contains("dotnet test test/Shop.Api.Tests", File.ReadAllText(Path.Combine(repo, ".gitlab-ci.yml")), StringComparison.Ordinal);
        var code = File.ReadAllText(Path.Combine(repo, "test", "Shop.Api.Tests", "PathTests.cs"));
        Assert.Contains("Path.Combine(root, \"test\", \"Shop.Api.Tests\")", code, StringComparison.Ordinal);
        Assert.Contains("\"test/Shop.Api.Tests/x\"", code, StringComparison.Ordinal);
        Assert.Contains("\"docs/tests/keep\"", code, StringComparison.Ordinal);
        Assert.Equal("ran tests/Shop.Api.Tests\n", File.ReadAllText(Path.Combine(repo, "docs", "evidence", "old.md")));
        var spec = File.ReadAllText(Path.Combine(repo, "docs", "specs", "contracts.md"));
        Assert.Contains("test/Shop.Api.Tests", spec, StringComparison.Ordinal);
        Assert.Contains("docs/tests/", spec, StringComparison.Ordinal);
        Assert.Contains("layout: v3", File.ReadAllText(Path.Combine(repo, ".net-arch", "project.yml")), StringComparison.Ordinal);
        Assert.Equal("v3", DoctorRunner.Run(repo).Layout);
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-A01")).Plan!);
    }

    [Fact]
    public void Layer_projects_are_created_with_only_the_allowed_references_and_registered_and_no_type_moves()
    {
        var repo = Repo();
        var runner = new FakeProcessRunner();
        var before = File.ReadAllText(Path.Combine(repo, "src", "Shop.Application", "Features", "Products", "ProductDto.cs"));

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), runner)))
            Run("fix", repo, apply: true, ("rules", "DA-A02"));

        var shared = File.ReadAllText(Path.Combine(repo, "src", "Shop.Domain.Shared", "Shop.Domain.Shared.csproj"));
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference", shared, StringComparison.Ordinal);
        Assert.Contains("Include=\"..\\Shop.Domain.Shared\\Shop.Domain.Shared.csproj\"", File.ReadAllText(Path.Combine(repo, "src", "Shop.Application.Contracts", "Shop.Application.Contracts.csproj")), StringComparison.Ordinal);
        var httpApi = File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi", "Shop.HttpApi.csproj"));
        Assert.Contains("Include=\"..\\Shop.Application.Contracts\\Shop.Application.Contracts.csproj\"", httpApi, StringComparison.Ordinal);
        Assert.Contains("<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />", httpApi, StringComparison.Ordinal);
        Assert.Contains("Include=\"..\\Shop.Application.Contracts\\Shop.Application.Contracts.csproj\"", File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Shop.HttpApi.Client.csproj")), StringComparison.Ordinal);
        Assert.Equal(4, runner.Calls.Count(c => c.Arguments.Contains("sln")));
        Assert.Equal(before, File.ReadAllText(Path.Combine(repo, "src", "Shop.Application", "Features", "Products", "ProductDto.cs")));
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-A02")).Plan!);
    }

    [Fact]
    public void Abp_rules_are_off_by_default_and_report_structure_and_code_findings_when_switched_on()
    {
        var repo = Repo();
        Assert.DoesNotContain(DoctorRunner.Run(repo).Findings, f => f.Id.StartsWith("DA-A", StringComparison.Ordinal));

        Write(repo, ".net-arch/project.yml", "schema: 2\nblueprint: 1.0.0\nlayout: v2\nstandards:\n- abp\n");
        var report = DoctorRunner.Run(repo);
        var ids = report.Findings.Select(f => f.Id).ToHashSet();

        Assert.Contains("DA-A01", ids);
        Assert.Contains("DA-A02", ids);
        Assert.Contains("DA-A04", ids);
        Assert.Contains("DA-A05", ids);
        Assert.DoesNotContain("DA-A06", ids);
        var repositoryIssues = report.Findings.Single(f => f.Id == "DA-A04").Details!;
        Assert.Contains(repositoryIssues, d => d.Contains("IProductRepository.Query", StringComparison.Ordinal) && d.Contains("returns IQueryable", StringComparison.Ordinal));
        Assert.Contains(repositoryIssues, d => d.Contains("IProductRepository.Find", StringComparison.Ordinal) && d.Contains("not async", StringComparison.Ordinal));
        Assert.Contains(repositoryIssues, d => d.Contains("IProductRepository.AddAsync", StringComparison.Ordinal) && d.Contains("no trailing CancellationToken", StringComparison.Ordinal));
        Assert.DoesNotContain(repositoryIssues, d => d.Contains("GetAsync", StringComparison.Ordinal));
        Assert.Contains(report.Findings.Single(f => f.Id == "DA-A05").Details!, d => d.Contains("PricingService", StringComparison.Ordinal));

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner())))
            Run("fix", repo, apply: true, ("rules", "DA-A02"));
        var after = DoctorRunner.Run(repo);
        Assert.DoesNotContain(after.Findings, f => f.Id == "DA-A02");
        Assert.Contains(after.Findings.Single(f => f.Id == "DA-A06").Details!, d => d.Contains("ProductDto", StringComparison.Ordinal));
    }

    [Fact]
    public void Reference_direction_of_the_layer_projects_is_checked()
    {
        var repo = Repo();
        Write(repo, ".net-arch/project.yml", "schema: 2\nblueprint: 1.0.0\nlayout: v3\nstandards:\n- abp\n");
        Write(repo, "src/Shop.Domain.Shared/Shop.Domain.Shared.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Write(repo, "src/Shop.Application.Contracts/Shop.Application.Contracts.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Domain\\Shop.Domain.csproj\" /></ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.HttpApi/Shop.HttpApi.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Application\\Shop.Application.csproj\" /></ItemGroup>\n</Project>\n");

        var finding = DoctorRunner.Run(repo).Findings.Single(f => f.Id == "DA-A03");

        Assert.Contains("Shop.Application.Contracts -> Shop.Domain", finding.Details!);
        Assert.Contains("Shop.HttpApi -> Shop.Application", finding.Details!);
    }

    [Fact]
    public void A_profile_can_switch_the_standard_on_and_raise_its_severity_and_exceptions_still_apply()
    {
        var repo = Repo();
        Write(repo, ".net-arch/profile.yml", "name: acme\nversion: 1.0.0\nstandards:\n- abp\nseverity:\n  DA-A02: error\n  DA-A01: error\n");

        var report = DoctorRunner.Run(repo);
        Assert.Equal(DoctorSeverity.Error, report.Findings.Single(f => f.Id == "DA-A02").Severity);
        Assert.Equal(DoctorSeverity.Error, report.Findings.Single(f => f.Id == "DA-A01").Severity);

        Write(repo, ".net-arch/rules.yml", "exceptions:\n- rule: DA-A01\n  reason: tests stay in tests/ until the next release\n");
        var accepted = DoctorRunner.Run(repo);
        Assert.DoesNotContain(accepted.Findings, f => f.Id == "DA-A01");
        Assert.Contains(accepted.Accepted, a => a.Id == "DA-A01");

        Write(repo, ".net-arch/rules.yml", "severity:\n  DA-A02: warning\n");
        Assert.Equal(DoctorSeverity.Warning, DoctorRunner.Run(repo).Findings.Single(f => f.Id == "DA-A02").Severity);
    }
}
