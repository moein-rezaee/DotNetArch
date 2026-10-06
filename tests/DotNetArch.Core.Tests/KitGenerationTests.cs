using System.Text.Json;
using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Kits;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

public sealed class KitGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-kits-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public KitGenerationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IDisposable Host() => ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

    private string P(string relative) => Path.Combine(_root, relative);

    private SolutionConfig Solution(OpsOptions? ops = null)
    {
        using var _ = Host();
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite",
            Ops: ops ?? new OpsOptions(Ci: "none", NoGit: true, NoDocker: true)));
        return ConfigManager.Load(P("Acme"))!;
    }

    [Fact]
    public void A_built_in_kit_has_abstractions_core_one_package_per_provider_docs_and_its_own_props()
    {
        using var _ = Host();
        var info = KitGenerator.Generate(new KitRequest(_root, "cache", new[] { "redis", "InMemory" }, "Acme", WithTests: true));

        Assert.Equal("Cache", info.Area);
        Assert.Equal(new[] { "Redis", "InMemory" }, info.Providers);
        var kit = P("kits/Cache");
        foreach (var file in new[]
        {
            "Acme.Kit.Cache.Abstractions/ICache.cs", "Acme.Kit.Cache.Abstractions/CacheException.cs",
            "Acme.Kit.Cache.Core/CacheProviderResolver.cs", "Acme.Kit.Cache.Core/DependencyInjection.cs",
            "Acme.Kit.Cache.Providers.Redis/RedisCacheProvider.cs", "Acme.Kit.Cache.Providers.InMemory/InMemoryCacheProvider.cs",
            "Directory.Build.props", "Directory.Packages.props", "README.md", "README.fa.md", "AGENTS.md", "kit.json",
            "docs/specs/overview.md", "docs/specs/overview.fa.md", "docs/specs/contracts.md", "docs/specs/acceptance.md", "docs/specs/changelog.md",
            "scripts/pack.sh", "tests/Acme.Kit.Cache.Tests/InMemoryCacheTests.cs",
        })
            Assert.True(File.Exists(Path.Combine(kit, file)), $"missing {file}");

        Assert.DoesNotContain("PackageReference", File.ReadAllText(Path.Combine(kit, "Acme.Kit.Cache.Abstractions/Acme.Kit.Cache.Abstractions.csproj")));
        Assert.DoesNotContain("Providers", File.ReadAllText(Path.Combine(kit, "Acme.Kit.Cache.Core/Acme.Kit.Cache.Core.csproj")));
        Assert.Contains("StackExchange.Redis", File.ReadAllText(Path.Combine(kit, "Acme.Kit.Cache.Providers.Redis/Acme.Kit.Cache.Providers.Redis.csproj")));
        Assert.Contains("<Version>1.0.0</Version>", File.ReadAllText(Path.Combine(kit, "Directory.Build.props")));
        Assert.Equal(new[] { "REDIS_PASSWORD" }, info.Secrets.Keys);
    }

    [Fact]
    public void Kits_never_depend_on_private_packages_or_on_the_hosting_solution()
    {
        using var _ = Host();
        KitGenerator.Generate(new KitRequest(_root, "MediaStorage", Array.Empty<string>(), "Acme"));

        var all = string.Join("\n", Directory.EnumerateFiles(P("kits"), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("Corevia", all);
        Assert.DoesNotContain("ErrorHandling", all);
        Assert.DoesNotContain("{{", all);
        Assert.Contains("MINIO_ACCESS_KEY", all);
        Assert.Contains("RUSTFS_SECRET_KEY", all);
    }

    [Fact]
    public void Naming_is_by_capability_never_by_product_and_the_area_is_normalised()
    {
        using var _ = Host();
        KitGenerator.Generate(new KitRequest(_root, "messageBroker", new[] { "RabbitMq" }, "Acme"));

        Assert.True(Directory.Exists(P("kits/MessageBroker/Acme.Kit.MessageBroker.Providers.RabbitMq")));
        Assert.False(Directory.Exists(P("kits/RabbitMq")) || Directory.Exists(P("kits/Redis")));
    }

    [Fact]
    public void Unknown_providers_of_a_built_in_area_and_existing_kits_are_rejected()
    {
        using var _ = Host();
        Assert.Throws<ArgumentException>(() => KitGenerator.Generate(new KitRequest(_root, "Cache", new[] { "Memcached" }, "Acme")));

        KitGenerator.Generate(new KitRequest(_root, "Cache", new[] { "InMemory" }, "Acme"));
        Assert.Throws<InvalidOperationException>(() => KitGenerator.Generate(new KitRequest(_root, "Cache", new[] { "InMemory" }, "Acme")));
    }

    [Fact]
    public void A_custom_area_gets_a_generic_skeleton_with_any_providers()
    {
        using var _ = Host();
        var info = KitGenerator.Generate(new KitRequest(_root, "Search", new[] { "Elastic", "Lucene" }, "Acme"));

        Assert.Equal(new[] { "Elastic", "Lucene" }, info.Providers);
        Assert.True(File.Exists(P("kits/Search/Acme.Kit.Search.Abstractions/ISearch.cs")));
        Assert.True(File.Exists(P("kits/Search/Acme.Kit.Search.Providers.Lucene/LuceneSearchProvider.cs")));
        Assert.Contains("TODO", File.ReadAllText(P("kits/Search/Acme.Kit.Search.Abstractions/ISearch.cs")));
    }

    [Fact]
    public void Invalid_names_are_rejected_before_anything_is_written()
    {
        using var _ = Host();
        Assert.Throws<ArgumentException>(() => KitGenerator.Generate(new KitRequest(_root, "Bad Area", Array.Empty<string>(), "Acme")));
        Assert.Throws<ArgumentException>(() => KitGenerator.Generate(new KitRequest(_root, "Cache", new[] { "bad;name" }, "Acme")));
        Assert.Throws<ArgumentException>(() => KitGenerator.Generate(new KitRequest(_root, "Search", Array.Empty<string>(), "Bad Prefix")));
        Assert.False(Directory.Exists(P("kits")));
    }

    [Fact]
    public void Wiring_puts_the_contract_in_application_and_everything_else_in_the_composition_root()
    {
        var config = Solution();
        using var _ = Host();
        var info = KitGenerator.Generate(new KitRequest(config.SolutionPath, "Cache", new[] { "InMemory", "Redis" }, "Acme"));
        Assert.True(KitWiring.Wire(config, info));

        var application = File.ReadAllText(P("Acme/src/Acme.Application/Acme.Application.csproj"));
        var api = File.ReadAllText(P("Acme/src/Acme.Api/Acme.Api.csproj"));
        Assert.Contains("Acme.Kit.Cache.Abstractions", application);
        Assert.DoesNotContain("Acme.Kit.Cache.Core", application);
        Assert.DoesNotContain("Providers", application);
        Assert.Contains("Acme.Kit.Cache.Core", api);
        Assert.Contains("Acme.Kit.Cache.Providers.Redis", api);

        var registration = File.ReadAllText(P("Acme/src/Acme.Api/Configuration/KitRegistrations.cs"));
        Assert.Contains("services.AddInMemoryCacheProvider();", registration);
        Assert.Contains("services.AddCacheKit(configuration);", registration);

        using var settings = JsonDocument.Parse(File.ReadAllText(P("Acme/src/Acme.Api/appsettings.json")));
        Assert.Equal("InMemory", settings.RootElement.GetProperty("Cache").GetProperty("Provider").GetString());
        Assert.Equal(0, settings.RootElement.GetProperty("Cache").GetProperty("Redis").GetProperty("Database").GetInt32());
        Assert.Contains("REDIS_PASSWORD=", File.ReadAllText(P("Acme/src/Acme.Api/.env.example")));
        Assert.Contains("\"REDIS_PASSWORD\"", File.ReadAllText(P("Acme/src/Acme.Api/Configuration/ConfigurationContract.cs")));
        Assert.Equal("InMemory,Redis", ConfigManager.Load(P("Acme"))!.Kits["Cache"]);
    }

    [Fact]
    public void Wiring_twice_does_not_duplicate_anything()
    {
        var config = Solution();
        using var _ = Host();
        var info = KitGenerator.Generate(new KitRequest(config.SolutionPath, "Cache", new[] { "Redis" }, "Acme"));
        KitWiring.Wire(config, info);
        KitWiring.Wire(config, info);

        var registration = File.ReadAllText(P("Acme/src/Acme.Api/Configuration/KitRegistrations.cs"));
        Assert.Equal(1, registration.Split("AddRedisCacheProvider").Length - 1);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(P("Acme/src/Acme.Api/Acme.Api.csproj")), "ProjectReference Include=\"[^\"]*Providers.Redis"));
    }

    [Fact]
    public void Wiring_a_kit_that_does_not_exist_fails_with_a_hint()
    {
        var config = Solution();
        using var _ = Host();
        Assert.False(KitWiring.WireFromDisk(config, "Cache"));
    }

    [Fact]
    public void New_service_without_logic_becomes_a_kit_and_with_logic_an_application_service()
    {
        var config = Solution();
        using var _ = Host();
        Assert.True(ServiceV2Generator.GenerateKit(config, "MessageBroker", new[] { "RabbitMq" }, withTests: false));
        Assert.True(File.Exists(P("Acme/kits/MessageBroker/kit.json")));
        Assert.False(Directory.Exists(P("Acme/kits/MessageBroker/tests")));

        Assert.True(ServiceV2Generator.GenerateInternal(config, null, "pricing", "scoped"));
        Assert.True(File.Exists(P("Acme/src/Acme.Application/Common/Services/Pricing.cs")));
        var di = File.ReadAllText(P("Acme/src/Acme.Application/DependencyInjection.cs"));
        Assert.Contains("services.AddScoped<IPricing, Pricing>();", di);
        Assert.Contains("using Acme.Application.Common.Services;", di);
        Assert.False(ServiceV2Generator.GenerateInternal(config, null, "Pricing", "Scoped"));
        Assert.Throws<ArgumentException>(() => ServiceV2Generator.GenerateInternal(config, null, "Other", "Forever"));
    }

    [Fact]
    public void An_internal_service_for_an_entity_lives_in_that_entitys_feature_folder()
    {
        var config = Solution();
        using var _ = Host();
        DotNetArch.Core.Scaffolding.Entities.CrudScaffolder.Generate(config, "Product");

        Assert.True(ServiceV2Generator.GenerateInternal(config, "Product", "Discounts", "Transient"));
        Assert.True(File.Exists(P("Acme/src/Acme.Application/Features/Products/Services/Discounts.cs")));
        Assert.False(ServiceV2Generator.GenerateInternal(config, "Ghost", "X", "Scoped"));
    }

    [Fact]
    public void Kit_publishing_jobs_exist_in_ci_only_when_a_nuget_feed_is_configured()
    {
        Solution(new OpsOptions(Ci: "github", NoGit: true, NoDocker: true));
        Assert.DoesNotContain("pack-kits", File.ReadAllText(P("Acme/.github/workflows/ci.yml")));
        Directory.Delete(P("Acme"), recursive: true);

        Solution(new OpsOptions(Ci: "github", NoGit: true, NoDocker: true, NuGetSource: "https://nuget.example.com/v3/index.json"));
        var ci = File.ReadAllText(P("Acme/.github/workflows/ci.yml"));
        Assert.Contains("bash scripts/pack-kits.sh", ci);
        Assert.Contains("secrets.NUGET_API_KEY", ci);
        Assert.True(File.Exists(P("Acme/scripts/pack-kits.sh")));
    }
}
