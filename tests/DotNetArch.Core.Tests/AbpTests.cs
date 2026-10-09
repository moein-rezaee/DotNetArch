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
        Write(repo, "src/Shop.Api/Services/ReportService.cs", "using Shop.Domain.Products;\n\nnamespace Shop.Api.Services;\npublic sealed class ReportService\n{\n    public ReportService(Product product) { }\n}\n");
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

    private string ApiRepo()
    {
        var repo = Path.Combine(_root, "api");
        Write(repo, "Shop.sln", "");
        Write(repo, "src/Shop.Domain/Shop.Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>\n</Project>\n");
        Write(repo, "src/Shop.Domain.Shared/Shop.Domain.Shared.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Write(repo, "src/Shop.Domain.Shared/Kind.cs", "namespace Shop.Domain;\npublic enum Kind { A = 1, B = 2 }\n");
        Write(repo, "src/Shop.Application.Contracts/Shop.Application.Contracts.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Write(repo, "src/Shop.Application.Contracts/ItemDto.cs", "namespace Shop.Application;\npublic sealed record ItemDto(string Id, Kind Kind);\n");
        Write(repo, "src/Shop.Application.Contracts/CreateItemRequest.cs", "namespace Shop.Api.Contracts;\npublic sealed record CreateItemRequest(string Title);\n");
        Write(repo, "src/Shop.HttpApi/Shop.HttpApi.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Write(repo, "src/Shop.HttpApi/Controllers/ItemController.cs", "using Microsoft.AspNetCore.Mvc;\nusing Shop.Application;\nusing Shop.Api.Contracts;\n\nnamespace Shop.Api.Controllers;\n\n[ApiController]\n[Route(\"v1/api/[controller]\")]\npublic sealed class ItemController : ControllerBase\n{\n    [HttpGet(\"{id}\")]\n    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]\n    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken) => Ok();\n\n    [HttpGet]\n    [ProducesResponseType(typeof(IReadOnlyCollection<ItemDto>), StatusCodes.Status200OK)]\n    public async Task<IActionResult> GetAll([FromQuery] Kind? kind, [FromQuery] List<string>? tags, int pageSize, CancellationToken cancellationToken) => Ok();\n\n    [HttpPost]\n    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status201Created)]\n    public async Task<IActionResult> Create([FromBody] CreateItemRequest request, CancellationToken cancellationToken) => Ok();\n\n    [HttpDelete(\"{id}\")]\n    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) => NoContent();\n\n    [HttpGet(\"export\")]\n    public async Task<IActionResult> Export(CancellationToken cancellationToken) => File(new byte[0], \"text/plain\");\n\n    [HttpGet(\"odd\")]\n    [ProducesResponseType(typeof(Unknown), StatusCodes.Status200OK)]\n    public async Task<IActionResult> Odd(CancellationToken cancellationToken) => Ok();\n}\n");
        return repo;
    }

    [Fact]
    public void Typed_client_is_generated_from_the_controllers_and_what_it_cannot_express_is_listed()
    {
        var repo = ApiRepo();
        var runner = new FakeProcessRunner();
        Assert.Contains("DA-A08", string.Join(',', DoctorRunnerWith(repo).Findings.Select(f => f.Id)));

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), runner)))
        {
            var plan = Run("fix", repo, apply: true, ("rules", "DA-A08"));
            Assert.Contains("ItemController.Odd skipped: response type Unknown", plan.Text, StringComparison.Ordinal);
        }

        var client = File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Item", "ItemClient.cs"));
        Assert.Contains("public interface IItemClient", client, StringComparison.Ordinal);
        Assert.Contains("Task<ItemDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);", client, StringComparison.Ordinal);
        Assert.Contains("var url = \"v1/api/Item/\" + ApiRoute.Segment(id);", client, StringComparison.Ordinal);
        Assert.Contains("GetFromJsonAsync<ItemDto>(url, cancellationToken)", client, StringComparison.Ordinal);
        Assert.Contains("ApiRoute.WithQuery(url, (\"kind\", kind), (\"tags\", tags), (\"pageSize\", pageSize))", client, StringComparison.Ordinal);
        Assert.Contains("PostAsJsonAsync(url, request, cancellationToken)", client, StringComparison.Ordinal);
        Assert.Contains("Task<ItemDto?> CreateAsync(CreateItemRequest request", client, StringComparison.Ordinal);
        Assert.Contains("DeleteAsync(url, cancellationToken)", client, StringComparison.Ordinal);
        Assert.Contains("GetByteArrayAsync(url, cancellationToken)", client, StringComparison.Ordinal);
        Assert.DoesNotContain("OddAsync", client, StringComparison.Ordinal);
        Assert.Contains("using Shop.Api.Contracts;", client, StringComparison.Ordinal);
        Assert.Contains("using Shop.Application;", client, StringComparison.Ordinal);
        Assert.Contains("Shop.Application.Contracts.csproj", File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Shop.HttpApi.Client.csproj")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Common", "ApiRoute.cs")));
        Assert.Contains("System.Collections.IEnumerable list and not string", File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Common", "ApiRoute.cs")), StringComparison.Ordinal);
        Assert.Equal(2, runner.Calls.Count(c => c.Arguments.Contains("sln")));
        var testFile = Directory.EnumerateFiles(repo, "ItemClientTests.cs", SearchOption.AllDirectories).Single();
        var clientTests = File.ReadAllText(testFile);
        Assert.Contains("Assert.Equal(HttpMethod.Get, handler.Method);", clientTests, StringComparison.Ordinal);
        Assert.Contains("Assert.Equal(\"/v1/api/Item/abc\", handler.Path);", clientTests, StringComparison.Ordinal);
        Assert.Contains("RouteParityTests", string.Join(' ', Directory.EnumerateFiles(Path.GetDirectoryName(testFile)!, "*.cs")), StringComparison.Ordinal);
        Assert.Contains("Skipped = 1", File.ReadAllText(Path.Combine(Path.GetDirectoryName(testFile)!, "RouteParityTests.cs")), StringComparison.Ordinal);
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-A08")).Plan!);
        Assert.DoesNotContain("DA-A08", string.Join(',', DoctorRunnerWith(repo).Findings.Select(f => f.Id)));
    }

    private static DoctorReport DoctorRunnerWith(string repo)
    {
        Write(repo, ".net-arch/project.yml", "schema: 2\nblueprint: 1.0.0\nlayout: v2\nstandards:\n- abp\n");
        return DoctorRunner.Run(repo);
    }

    [Fact]
    public void Api_models_belong_to_application_contracts_and_models_that_need_aspnet_stay_with_the_controllers()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Api/Contracts/Products/CreateProductRequest.cs", "using Shop.Application.Features.Products;\n\nnamespace Shop.Api.Contracts.Products;\npublic sealed record CreateProductRequest(int Id)\n{\n    public GetProductQuery ToQuery() => new(Id);\n}\n");
        Write(repo, "src/Shop.Api/Contracts/Products/UploadRequest.cs", "using Microsoft.AspNetCore.Http;\n\nnamespace Shop.Api.Contracts.Products;\npublic sealed class UploadRequest\n{\n    public IFormFile? File { get; set; }\n}\n");

        var plan = Run("fix", repo, apply: false, ("rules", "DA-A02"));

        Assert.Contains(plan.Plan!, c => c.Path == "src/Shop.Application.Contracts/Contracts/Products/CreateProductRequest.cs");
        Assert.DoesNotContain(plan.Plan!, c => c.Path.EndsWith("UploadRequest.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_file_moves_to_etc_docker_with_its_relative_paths_re_based()
    {
        var repo = Repo();
        Write(repo, "docker-compose.yml", "services:\n  api:\n    build:\n      context: .\n      dockerfile: src/Shop.Api/Dockerfile\n    env_file:\n    - ./src/Shop.Api/.env\n    volumes:\n    - type: bind\n      source: ./src/Shop.Api/appsettings.json\n      target: /app/appsettings.json\n    - ./data:/data\n    - named:/cache\n    ports:\n    - 5000:5000\n");
        Write(repo, "README.md", "Run `docker compose -f docker-compose.yml up` from the root.\n");
        Write(repo, "docs/evidence/old.md", "see docker-compose.yml\n");

        var plan = Run("fix", repo, apply: true, ("rules", "DA-A09"));

        Assert.False(File.Exists(Path.Combine(repo, "docker-compose.yml")));
        var compose = File.ReadAllText(Path.Combine(repo, "etc", "docker", "docker-compose.yml"));
        Assert.Contains("context: ../..", compose, StringComparison.Ordinal);
        Assert.Contains("dockerfile: src/Shop.Api/Dockerfile", compose, StringComparison.Ordinal);
        Assert.Contains("- ../../src/Shop.Api/.env", compose, StringComparison.Ordinal);
        Assert.Contains("source: ../../src/Shop.Api/appsettings.json", compose, StringComparison.Ordinal);
        Assert.Contains("- ../../data:/data", compose, StringComparison.Ordinal);
        Assert.Contains("- named:/cache", compose, StringComparison.Ordinal);
        Assert.Contains("5000:5000", compose, StringComparison.Ordinal);
        Assert.Contains("-f etc/docker/docker-compose.yml up", File.ReadAllText(Path.Combine(repo, "README.md")), StringComparison.Ordinal);
        Assert.Equal("see docker-compose.yml\n", File.ReadAllText(Path.Combine(repo, "docs", "evidence", "old.md")));
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-A09")).Plan!);
        Assert.DoesNotContain(plan.Plan!, c => c.Path == "docker-compose.yml" && c.Action == "modify");
    }

    [Fact]
    public void Typed_client_can_use_the_rest_client_abstraction_a_profile_names_instead_of_HttpClient()
    {
        var repo = ApiRepo();
        Write(repo, ".net-arch/profile.yml", "name: acme\nversion: 1.0.0\nclient:\n  transport: rest-client\n  interface: IRestClient\n  namespace: Acme.Http.Abstractions\n  package: Acme.Http.Abstractions\n");

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner())))
        {
            var plan = Run("fix", repo, apply: true, ("rules", "DA-A08"));
            Assert.Contains("ItemController.Export skipped: the response is raw bytes", plan.Text, StringComparison.Ordinal);
        }

        var client = File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Item", "ItemClient.cs"));
        Assert.Contains("using Acme.Http.Abstractions;", client, StringComparison.Ordinal);
        Assert.Contains("public ItemClient(IRestClient rest)", client, StringComparison.Ordinal);
        Assert.Contains("var json = await _rest.GetAsync(url, null, null, cancellationToken).ConfigureAwait(false);", client, StringComparison.Ordinal);
        Assert.Contains("return ApiJson.Read<ItemDto>(json);", client, StringComparison.Ordinal);
        Assert.Contains("await _rest.PostAsync(url, request, null, null, cancellationToken)", client, StringComparison.Ordinal);
        Assert.Contains("await _rest.DeleteAsync(url, null, null, cancellationToken)", client, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", client, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportAsync", client, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Acme.Http.Abstractions\" />", File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Shop.HttpApi.Client.csproj")), StringComparison.Ordinal);
        Assert.Contains("internal static class ApiJson", File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi.Client", "Common", "ApiRoute.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Host_services_move_to_the_http_layer_with_internals_visible_to_the_host()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Api/Services/ExportService.cs", "using MediatR;\n\nnamespace Shop.Api.Services;\ninternal sealed class ExportService\n{\n    public ExportService(IMediator mediator) { }\n}\n");
        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner())))
            Run("fix", repo, apply: true, ("rules", "DA-A02"));

        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.HttpApi", "Services", "ExportService.cs")));
        Assert.False(Directory.Exists(Path.Combine(repo, "src", "Shop.Api", "Services")));
        Assert.Contains("<InternalsVisibleTo Include=\"Shop.Api\" />", File.ReadAllText(Path.Combine(repo, "src", "Shop.HttpApi", "Shop.HttpApi.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Loose_files_and_crowded_folders_are_flagged_and_moved_into_feature_and_kind_folders_inside_their_project()
    {
        var repo = Repo();
        Write(repo, ".net-arch/project.yml", "schema: 2\nblueprint: 1.0.0\nlayout: v3\nstandards:\n- abp\n");
        Write(repo, "src/Shop.Application/Features/Products/Marker.cs", "namespace Shop.Application.Features.Products;\npublic sealed class Marker { }\n");
        Write(repo, "src/Shop.Application/Features/Orders/Marker.cs", "namespace Shop.Application.Features.Orders;\npublic sealed class Marker2 { }\n");
        Write(repo, "src/Shop.Application/ShopOptions.cs", "namespace Shop.Application;\npublic sealed class ShopOptions { }\n");
        Write(repo, "src/Shop.Application/AssemblyMarker.cs", "namespace Shop.Application;\npublic sealed class AssemblyMarker { }\n");
        for (var i = 0; i < 14; i++)
            Write(repo, $"src/Shop.Domain/Ports/{(i % 2 == 0 ? "IProduct" : "IOrder")}Thing{i}.cs", $"namespace Shop.Domain.Ports;\npublic interface {(i % 2 == 0 ? "IProduct" : "IOrder")}Thing{i} {{ }}\n");

        Assert.Contains(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-A10" && f.Details!.Any(d => d.Contains("ShopOptions.cs", StringComparison.Ordinal)));

        Run("fix", repo, apply: true, ("rules", "DA-A10"));

        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Application", "Options", "ShopOptions.cs")));
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Application", "AssemblyMarker.cs")));
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Domain", "Ports", "Products", "IProductThing0.cs")));
        Assert.True(File.Exists(Path.Combine(repo, "src", "Shop.Domain", "Ports", "Orders", "IOrderThing1.cs")));
        Assert.Contains("namespace Shop.Domain.Ports;", File.ReadAllText(Path.Combine(repo, "src", "Shop.Domain", "Ports", "Products", "IProductThing0.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-A10");
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-A10")).Plan!);
    }

    [Fact]
    public void Missing_references_are_added_forbidden_unused_ones_removed_and_the_solution_and_dockerfile_follow()
    {
        var repo = Repo();
        Write(repo, ".net-arch/project.yml", "schema: 2\nblueprint: 1.0.0\nlayout: v3\nstandards:\n- abp\n");
        Write(repo, "src/Shop.Domain.Shared/Shop.Domain.Shared.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Write(repo, "src/Shop.Domain.Shared/Kinds/Color.cs", "namespace Shop.Domain.Shared;\npublic enum Color { Red }\n");
        Write(repo, "src/Shop.Application.Contracts/Shop.Application.Contracts.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Domain\\Shop.Domain.csproj\" /></ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.Application.Contracts/Dtos/PaintDto.cs", "using Shop.Domain.Shared;\n\nnamespace Shop.Application.Contracts;\npublic sealed record PaintDto(Color Color);\n");

        var finding = DoctorRunner.Run(repo).Findings.Single(f => f.Id == "DA-A11");
        Assert.Contains(finding.Details!, d => d.StartsWith("missing: Shop.Application.Contracts -> Shop.Domain.Shared", StringComparison.Ordinal));
        Assert.Contains(finding.Details!, d => d.StartsWith("forbidden: Shop.Application.Contracts -> Shop.Domain", StringComparison.Ordinal));
        Assert.Contains(finding.Details!, d => d.StartsWith("solution: Shop.Domain.Shared", StringComparison.Ordinal));

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner())))
            Run("fix", repo, apply: true, ("rules", "DA-A11"));

        var csproj = File.ReadAllText(Path.Combine(repo, "src", "Shop.Application.Contracts", "Shop.Application.Contracts.csproj"));
        Assert.Contains("Shop.Domain.Shared.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Shop.Domain.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-A11" && f.Details!.Any(d => d.StartsWith("missing", StringComparison.Ordinal) || d.StartsWith("forbidden", StringComparison.Ordinal)));
    }
}
