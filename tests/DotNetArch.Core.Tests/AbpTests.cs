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
        Write(repo, "src/Shop.Application/Shop.Application.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Domain\\Shop.Domain.csproj\" /></ItemGroup>\n  <ItemGroup><PackageReference Include=\"MediatR\" Version=\"12.2.0\" /></ItemGroup>\n  <ItemGroup><InternalsVisibleTo Include=\"Shop.Application.Tests\" /></ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.Domain/Products/ProductKind.cs", "namespace Shop.Domain.Products;\npublic enum ProductKind\n{\n    Physical = 1,\n    Digital = 2,\n}\n");
        Write(repo, "src/Shop.Application/Features/Products/ProductDto.cs", "using Shop.Domain.Products;\n\nnamespace Shop.Application.Features.Products;\npublic sealed record ProductDto(int Id, ProductKind Kind);\n");
        Write(repo, "src/Shop.Application/Features/Products/GetProductQuery.cs", "using MediatR;\n\nnamespace Shop.Application.Features.Products;\npublic sealed record GetProductQuery(int Id) : IRequest<ProductDto>;\n");
        Write(repo, "src/Shop.Application/Features/Products/GetProductQueryHandler.cs", "using MediatR;\nusing Shop.Domain.Products;\n\nnamespace Shop.Application.Features.Products;\npublic sealed class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDto>\n{\n    public Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken) => Task.FromResult(new ProductDto(request.Id, ProductKind.Physical));\n}\n");
        Write(repo, "src/Shop.Api/Controllers/ProductController.cs", "using MediatR;\nusing Microsoft.AspNetCore.Mvc;\nusing Shop.Application.Features.Products;\n\nnamespace Shop.Api.Controllers;\n[ApiController]\npublic sealed class ProductController : ControllerBase\n{\n    private readonly IMediator _mediator;\n\n    public ProductController(IMediator mediator) { _mediator = mediator; }\n\n    [HttpGet]\n    public async Task<IActionResult> Get(int id) => Ok(await _mediator.Send(new GetProductQuery(id)));\n}\n");
        Write(repo, "src/Shop.Domain/Products/IProductRepository.cs", "namespace Shop.Domain.Products;\npublic interface IProductRepository\n{\n    Task<Product?> GetAsync(int id, CancellationToken cancellationToken = default);\n    IQueryable<Product> Query();\n    Product Find(int id);\n    Task AddAsync(Product product);\n}\npublic sealed class Product { }\npublic sealed class PricingService { }\n");
        Write(repo, "src/Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Application\\Shop.Application.csproj\" /></ItemGroup>\n  <ItemGroup><PackageReference Include=\"MediatR\" Version=\"12.2.0\" /></ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.Api/Dockerfile", "COPY src/Shop.Domain/Shop.Domain.csproj src/Shop.Domain/\nCOPY src/Shop.Application/Shop.Application.csproj src/Shop.Application/\nCOPY src/Shop.Api/Shop.Api.csproj src/Shop.Api/\nCOPY tests/Shop.Api.Tests/Shop.Api.Tests.csproj tests/Shop.Api.Tests/\n");
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
    public void Layers_are_created_only_where_files_move_in_and_the_move_keeps_namespaces_and_wires_everything()
    {
        var repo = Repo();
        var runner = new FakeProcessRunner();
        var dto = File.ReadAllText(Path.Combine(repo, "src", "Shop.Application", "Features", "Products", "ProductDto.cs"));
        var controller = File.ReadAllText(Path.Combine(repo, "src", "Shop.Api", "Controllers", "ProductController.cs"));

        var plan = Run("fix", repo, apply: false, ("rules", "DA-A02"));
        Assert.Contains(plan.Plan!, c => c.Action == "move" && c.Path == "src/Shop.Domain.Shared/Products/ProductKind.cs");
        Assert.DoesNotContain(plan.Plan!, c => c.Path.Contains("HttpApi.Client", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Domain", "Products", "ProductKind.cs")));

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), runner)))
            Run("fix", repo, apply: true, ("rules", "DA-A02"));

        Assert.Equal(dto, File.ReadAllText(Path.Combine(repo, "src", "Shop.Application.Contracts", "Features", "Products", "ProductDto.cs")));
        Assert.Equal(controller, File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi", "Controllers", "ProductController.cs")));
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Application.Contracts", "Features", "Products", "GetProductQuery.cs")));
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Application", "Features", "Products", "GetProductQueryHandler.cs")));
        Assert.False(Directory.Exists(Path.Combine(repo, "src", "Shop.HttpApi.Client")));
        Assert.False(Directory.Exists(Path.Combine(repo, "src", "Shop.Api", "Controllers")));
        var contracts = File.ReadAllText(Path.Combine(repo, "src", "Shop.Application.Contracts", "Shop.Application.Contracts.csproj"));
        Assert.Contains("Shop.Domain.Shared.csproj", contracts, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"MediatR\" Version=\"12.2.0\" />", contracts, StringComparison.Ordinal);
        Assert.Contains("InternalsVisibleTo Include=\"Shop.Application.Tests\"", contracts, StringComparison.Ordinal);
        var httpApi = File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi", "Shop.HttpApi.csproj"));
        Assert.Contains("Shop.Application.Contracts.csproj", httpApi, StringComparison.Ordinal);
        Assert.Contains("<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />", httpApi, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"MediatR\" Version=\"12.2.0\" />", httpApi, StringComparison.Ordinal);
        Assert.Contains("Shop.Domain.Shared.csproj", File.ReadAllText(Path.Combine(repo, "src", "Shop.Domain", "Shop.Domain.csproj")), StringComparison.Ordinal);
        Assert.Contains("Shop.Application.Contracts.csproj", File.ReadAllText(Path.Combine(repo, "src", "Shop.Application", "Shop.Application.csproj")), StringComparison.Ordinal);
        Assert.Contains("Shop.HttpApi.csproj", File.ReadAllText(Path.Combine(repo, "src", "Shop.Api", "Shop.Api.csproj")), StringComparison.Ordinal);
        var dockerfile = File.ReadAllText(Path.Combine(repo, "src", "Shop.Api", "Dockerfile"));
        Assert.Contains("COPY src/Shop.Domain.Shared/Shop.Domain.Shared.csproj src/Shop.Domain.Shared/", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY src/Shop.Application.Contracts/Shop.Application.Contracts.csproj src/Shop.Application.Contracts/", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY src/Shop.HttpApi/Shop.HttpApi.csproj src/Shop.HttpApi/", dockerfile, StringComparison.Ordinal);
        Assert.Equal(3, runner.Calls.Count(c => c.Arguments.Contains("sln")));
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-A02")).Plan!);
    }

    [Fact]
    public void Empty_layers_are_created_only_when_asked_for()
    {
        var repo = Repo();
        File.Delete(Path.Combine(repo, "src", "Shop.Api", "Controllers", "ProductController.cs"));
        File.Delete(Path.Combine(repo, "src", "Shop.Domain", "Products", "ProductKind.cs"));

        var plan = Run("fix", repo, apply: false, ("rules", "DA-A02"));
        Assert.DoesNotContain(plan.Plan!, c => c.Path.Contains("Shop.HttpApi", StringComparison.Ordinal) || c.Path.Contains("Domain.Shared", StringComparison.Ordinal));

        var forced = Run("fix", repo, apply: false, ("rules", "DA-A02"), ("empty", "true"));
        Assert.Contains(forced.Plan!, c => c.Path == "src/Shop.HttpApi.Client/Shop.HttpApi.Client.csproj" && c.Reason.Contains("empty", StringComparison.Ordinal));
        Assert.Contains(forced.Plan!, c => c.Path == "src/Shop.HttpApi/Shop.HttpApi.csproj");
    }

    [Fact]
    public void One_controller_that_cannot_move_keeps_all_controllers_together_and_the_reason_is_listed()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Api/Services/ReportService.cs", "namespace Shop.Api.Services;\npublic sealed class ReportService\n{\n    public string Name => \"report\";\n}\n");
        Write(repo, "src/Shop.Api/Controllers/ReportController.cs", "using Microsoft.AspNetCore.Mvc;\nusing Shop.Api.Services;\n\nnamespace Shop.Api.Controllers;\n[ApiController]\npublic sealed class ReportController : ControllerBase\n{\n    public ReportController(ReportService service) { }\n}\n");

        var plan = Run("fix", repo, apply: false, ("rules", "DA-A02"));

        Assert.DoesNotContain(plan.Plan!, c => c.Path.Contains("Shop.HttpApi", StringComparison.Ordinal));
        Assert.Contains("ReportController.cs stays", plan.Text, StringComparison.Ordinal);
        Assert.Contains("controllers move together", plan.Text, StringComparison.Ordinal);
        Assert.Contains(plan.Plan!, c => c.Path == "src/Shop.Application.Contracts/Features/Products/ProductDto.cs");
    }

    [Fact]
    public void A_property_named_like_a_type_is_not_a_dependency_on_that_type()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Domain/Products/Currency.cs", "namespace Shop.Domain.Products;\npublic sealed class Currency\n{\n    public string Code { get; init; } = string.Empty;\n    public void Touch() { }\n}\n");
        Write(repo, "src/Shop.Application/Features/Products/PriceDto.cs", "namespace Shop.Application.Features.Products;\npublic sealed record PriceDto(decimal Value, string Currency);\n");

        var plan = Run("fix", repo, apply: false, ("rules", "DA-A02"));

        Assert.Contains(plan.Plan!, c => c.Path == "src/Shop.Application.Contracts/Features/Products/PriceDto.cs");
        Assert.DoesNotContain(plan.Plan!, c => c.Path.EndsWith("Currency.cs", StringComparison.Ordinal));
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
        Assert.DoesNotContain("DA-A07", ids);
        Assert.Contains("Application.Contracts", report.Findings.Single(f => f.Id == "DA-A02").Message, StringComparison.Ordinal);
        var repositoryIssues = report.Findings.Single(f => f.Id == "DA-A04").Details!;
        Assert.Contains(repositoryIssues, d => d.Contains("IProductRepository.Query", StringComparison.Ordinal) && d.Contains("returns IQueryable", StringComparison.Ordinal));
        Assert.Contains(repositoryIssues, d => d.Contains("IProductRepository.Find", StringComparison.Ordinal) && d.Contains("not async", StringComparison.Ordinal));
        Assert.Contains(repositoryIssues, d => d.Contains("IProductRepository.AddAsync", StringComparison.Ordinal) && d.Contains("no trailing CancellationToken", StringComparison.Ordinal));
        Assert.DoesNotContain(repositoryIssues, d => d.Contains("GetAsync", StringComparison.Ordinal));
        Assert.Contains(report.Findings.Single(f => f.Id == "DA-A05").Details!, d => d.Contains("PricingService", StringComparison.Ordinal));

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner())))
            Run("fix", repo, apply: true, ("rules", "DA-A02"));
        var after = DoctorRunner.Run(repo);
        Assert.DoesNotContain(after.Findings, f => f.Id is "DA-A02" or "DA-A06" or "DA-A07");

        Directory.CreateDirectory(Path.Combine(repo, "src", "Shop.HttpApi.Client"));
        File.WriteAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Shop.HttpApi.Client.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Assert.Contains(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-A07" && f.Details!.Contains("Shop.HttpApi.Client"));
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
