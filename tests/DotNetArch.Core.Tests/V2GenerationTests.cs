using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>Generates layout-v2 solutions with a fake process runner (no SDK, no network) and checks structure and rules.</summary>
public sealed class V2GenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-v2-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public V2GenerationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IDisposable Host() => ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

    private SolutionConfig Generate(string provider = "SQLite")
    {
        using var _ = Host();
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", provider));
        return ConfigManager.Load(Path.Combine(_root, "Acme"))!;
    }

    private string Path_(string relative) => Path.Combine(_root, "Acme", relative);

    private string Read(string relative) => File.ReadAllText(Path_(relative));

    [Fact]
    public void Solution_has_the_four_layers_wired_per_layer()
    {
        var config = Generate();

        Assert.True(config.IsV2);
        Assert.Equal("Acme.Api", config.StartupProject);
        foreach (var file in new[]
        {
            "global.json", "Directory.Build.props", "Directory.Packages.props", ".config/dotnet-tools.json", ".gitignore",
            "src/Acme.Domain/Acme.Domain.csproj", "src/Acme.Application/Acme.Application.csproj",
            "src/Acme.Infrastructure/Acme.Infrastructure.csproj", "src/Acme.Api/Acme.Api.csproj",
            "src/Acme.Application/DependencyInjection.cs", "src/Acme.Infrastructure/DependencyInjection.cs",
            "src/Acme.Api/Program.cs", "src/Acme.Api/.env.example", "src/Acme.Api/appsettings.example.json",
        })
            Assert.True(File.Exists(Path_(file)), $"missing {file}");

        var domain = Read("src/Acme.Domain/Acme.Domain.csproj");
        var application = Read("src/Acme.Application/Acme.Application.csproj");
        var infrastructure = Read("src/Acme.Infrastructure/Acme.Infrastructure.csproj");
        var api = Read("src/Acme.Api/Acme.Api.csproj");

        Assert.DoesNotContain("ProjectReference", domain);
        Assert.DoesNotContain("Acme.Infrastructure", application);
        Assert.DoesNotContain("EntityFrameworkCore", application);
        Assert.Contains("Acme.Application", infrastructure);
        Assert.DoesNotContain("EntityFrameworkCore.Design", api);
        Assert.Contains("Acme.Infrastructure", api);
    }

    [Fact]
    public void No_template_token_is_left_unresolved()
    {
        Generate();
        using var _ = Host();
        CrudScaffolder.Generate(ConfigManager.Load(Path_(""))!, "Product");

        var offenders = Directory.EnumerateFiles(Path_(""), "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .Where(file => File.ReadAllText(file).Contains("{{"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("SQLite", "Microsoft.EntityFrameworkCore.Sqlite", "UseSqlite")]
    [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer", "UseSqlServer")]
    [InlineData("Postgres", "Npgsql.EntityFrameworkCore.PostgreSQL", "UseNpgsql")]
    public void Provider_choice_only_changes_infrastructure(string provider, string package, string useCall)
    {
        Generate(provider);

        Assert.Contains(package, Read("src/Acme.Infrastructure/Acme.Infrastructure.csproj"));
        Assert.Contains(package, Read("Directory.Packages.props"));
        Assert.Contains(useCall, Read("src/Acme.Infrastructure/DependencyInjection.cs"));
        Assert.Contains(useCall, Read("src/Acme.Infrastructure/Persistence/DesignTimeDbContextFactory.cs"));
        Assert.DoesNotContain(package, Read("src/Acme.Application/Acme.Application.csproj"));
        Assert.DoesNotContain("<PackageVersion Include=\"Microsoft.EntityFrameworkCore.Sqlite\" Version=\"" + "x", Read("Directory.Packages.props"));
    }

    [Fact]
    public void Unsupported_provider_is_rejected()
    {
        using var _ = Host();
        Assert.Throws<ArgumentException>(() =>
            SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "None")));
    }

    [Fact]
    public void Configuration_keeps_secrets_in_the_environment_and_settings_in_appsettings()
    {
        Generate();

        var settings = Read("src/Acme.Api/appsettings.json");
        Assert.DoesNotContain("ConnectionString", settings);
        Assert.Contains("DATABASE_CONNECTION_STRING", Read("src/Acme.Api/.env.example"));
        Assert.Contains("AddEnvironmentVariables", Read("src/Acme.Infrastructure/Configuration/AppConfigurationExtensions.cs"));
        Assert.Contains("EnvFile.Read", Read("src/Acme.Infrastructure/Configuration/AppConfigurationExtensions.cs"));
        Assert.Contains(".env", Read(".gitignore"));
    }

    [Fact]
    public void Crud_creates_a_nested_slice_per_entity()
    {
        var config = Generate();
        using var _ = Host();
        CrudScaffolder.Generate(config, "Category");

        var feature = "src/Acme.Application/Features/Categories";
        foreach (var path in new[]
        {
            $"{feature}/Commands/CreateCategory/CreateCategoryCommand.cs",
            $"{feature}/Commands/UpdateCategory/UpdateCategoryCommandHandler.cs",
            $"{feature}/Commands/DeleteCategory/DeleteCategoryCommand.cs",
            $"{feature}/Queries/GetCategoryById/GetCategoryByIdQuery.cs",
            $"{feature}/Queries/GetCategories/GetCategoriesQueryHandler.cs",
            $"{feature}/Dtos/CategoryDtos.cs",
            "src/Acme.Domain/Entities/Category.cs",
            "src/Acme.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs",
            "src/Acme.Api/Controllers/Categories/CategoriesController.cs",
        })
            Assert.True(File.Exists(Path_(path)), $"missing {path}");

        Assert.True(ConfigManager.Load(Path_(""))!.Entities["Category"].HasCrud);
        Assert.Contains("[Route(\"api/categories\")]", Read("src/Acme.Api/Controllers/Categories/CategoriesController.cs"));
    }

    [Fact]
    public void Crud_is_idempotent_and_never_overwrites_edited_files()
    {
        var config = Generate();
        using var _ = Host();
        CrudScaffolder.Generate(config, "Product");
        var entityPath = Path_("src/Acme.Domain/Entities/Product.cs");
        File.WriteAllText(entityPath, File.ReadAllText(entityPath) + "// edited by hand\n");

        CrudScaffolder.Generate(config, "Product");

        Assert.EndsWith("// edited by hand\n", File.ReadAllText(entityPath));
    }

    [Fact]
    public void Action_event_enum_and_constant_land_inside_the_entity_slice()
    {
        var config = Generate();
        using var _ = Host();
        CrudScaffolder.Generate(config, "Product");
        CrudScaffolder.Generate(config, "Order");

        ActionScaffolder.Generate(config, "Product", "Archive", "POST", crudStyle: false);
        ActionScaffolder.Generate(config, "Product", "Summary", "GET", crudStyle: false);
        Assert.True(EventScaffolder.GenerateEvent(config, "Product", "Created"));
        Assert.True(EventScaffolder.AddSubscriber(config, "Product", "Created", "Order"));
        Assert.True(EnumScaffolder.Generate(config, "Product", "ProductStatus"));
        Assert.True(EnumScaffolder.Generate(config, null, "Currency"));
        Assert.True(ConstantScaffolder.Generate(config, "Order", "OrderLimits"));

        Assert.True(File.Exists(Path_("src/Acme.Application/Features/Products/Actions/ArchiveProduct/ArchiveProductCommandHandler.cs")));
        Assert.True(File.Exists(Path_("src/Acme.Application/Features/Products/Actions/SummaryProduct/SummaryProductQueryHandler.cs")));
        Assert.True(File.Exists(Path_("src/Acme.Domain/Entities/Product.Archive.cs")));
        Assert.Contains("[HttpPost(\"{id:guid}/archive\")]", Read("src/Acme.Api/Controllers/Products/ProductsController.Archive.cs"));
        Assert.Contains("[HttpGet(\"{id:guid}/summary\")]", Read("src/Acme.Api/Controllers/Products/ProductsController.Summary.cs"));
        Assert.Equal(new[] { "Created" }, EventScaffolder.ListEvents(config, "Product"));
        Assert.True(File.Exists(Path_("src/Acme.Application/Features/Orders/Events/OnProductCreatedHandler.cs")));
        Assert.Contains("namespace Acme.Domain.Enums.Products;", Read("src/Acme.Domain/Enums/Products/ProductStatus.cs"));
        Assert.Contains("namespace Acme.Domain.Enums;", Read("src/Acme.Domain/Enums/Currency.cs"));
        Assert.Contains("class OrderLimits", Read("src/Acme.Domain/Constants/Orders/OrderLimits.cs"));
    }

    [Fact]
    public void Action_requires_an_existing_entity_and_a_name()
    {
        var config = Generate();
        using var _ = Host();
        ActionScaffolder.Generate(config, "Ghost", "Archive", "POST", crudStyle: false);
        Assert.False(File.Exists(Path_("src/Acme.Application/Features/Ghosts/Actions/ArchiveGhost/ArchiveGhostCommand.cs")));

        CrudScaffolder.Generate(config, "Product");
        ActionScaffolder.Generate(config, "Product", "Create", "POST", crudStyle: true);
        Assert.False(Directory.Exists(Path_("src/Acme.Application/Features/Products/Actions")));
    }

    [Fact]
    public void Generated_code_avoids_the_defects_found_in_the_reference_sample()
    {
        var config = Generate();
        using var _ = Host();
        CrudScaffolder.Generate(config, "Product");

        var source = string.Join("\n", Directory.EnumerateFiles(Path_("src"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("GetAwaiter().GetResult()", source);
        Assert.DoesNotContain("AllowAnyOrigin", source);
        Assert.DoesNotContain("Console.Write", source);
        Assert.DoesNotContain("StackTrace", source);
        Assert.DoesNotMatch(@"IQueryable<\w", Read("src/Acme.Application/Abstractions/Persistence/IRepository.cs"));
        Assert.DoesNotContain("{ get; set; }", Read("src/Acme.Domain/Entities/Product.cs"));
    }

    [Fact]
    public void Every_layer_gets_a_test_project_and_each_crud_slice_gets_tests()
    {
        var config = Generate();
        using var _ = Host();
        CrudScaffolder.Generate(config, "Product");

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Api" })
            Assert.True(File.Exists(Path_($"tests/Acme.{layer}.Tests/Acme.{layer}.Tests.csproj")), $"missing {layer} tests");

        Assert.True(File.Exists(Path_("tests/Acme.Domain.Tests/Entities/ProductTests.cs")));
        Assert.True(File.Exists(Path_("tests/Acme.Application.Tests/Features/Products/ProductHandlerTests.cs")));
        Assert.True(File.Exists(Path_("tests/Acme.Infrastructure.Tests/Persistence/ProductPersistenceTests.cs")));
        Assert.True(File.Exists(Path_("tests/Acme.Api.Tests/Features/ProductsApiTests.cs")));
        Assert.Contains("[Trait(\"Category\", \"Configuration\")]", Read("tests/Acme.Api.Tests/Configuration/ConfigurationExamplesTests.cs"));
        Assert.Contains("tests/Acme.Domain.Tests", string.Join("\n", _runner.Calls.Select(call => string.Join(' ', call.Arguments))));
    }

    [Fact]
    public void API_tests_that_need_a_real_database_are_only_generated_for_sqlite()
    {
        var config = Generate("Postgres");
        using var _ = Host();
        CrudScaffolder.Generate(config, "Product");

        Assert.True(File.Exists(Path_("tests/Acme.Application.Tests/Features/Products/ProductHandlerTests.cs")));
        Assert.False(File.Exists(Path_("tests/Acme.Api.Tests/Features/ProductsApiTests.cs")));
    }

    [Fact]
    public void No_tests_flag_skips_test_projects_and_slice_tests()
    {
        using (var _ = Host())
            SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite",
                Ops: new DotNetArch.Core.Scaffolding.Ops.OpsOptions(Ci: "none", NoGit: true, NoDocker: true, NoTests: true)));
        var config = ConfigManager.Load(Path_(""))!;
        using var __ = Host();
        CrudScaffolder.Generate(config, "Product");

        Assert.False(Directory.Exists(Path_("tests")));
    }
}
