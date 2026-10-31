using DotNetArch.Core.Doctor;
using DotNetArch.Core.Operations;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>move_code, add_composition and the adapters rule set (DA-AD*, Phase 15): a module becomes composable without a hand edit of the mechanical parts.</summary>
public sealed class AdapterOperationsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-adapters-" + Guid.NewGuid().ToString("N"));

    public AdapterOperationsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Write(string repo, string relative, string content)
    {
        var path = Path.Combine(repo, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Csproj(string[] references, params string[] extra) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n"
        + string.Concat(references.Select(r => $"    <ProjectReference Include=\"..\\{r}\\{r}.csproj\" />\n"))
        + "  </ItemGroup>\n" + string.Concat(extra) + "</Project>\n";

    private string Repo(string standards = "adapters", string? profile = null)
    {
        var repo = Path.Combine(_root, "shop");
        Write(repo, "src/Shop.Domain/Shop.Domain.csproj", Csproj(Array.Empty<string>()));
        Write(repo, "src/Shop.Domain/Ports/IPriceSource.cs", "namespace Shop.Domain.Ports;\npublic interface IPriceSource { decimal Get(int id); }\n");
        Write(repo, "src/Shop.Application/Shop.Application.csproj", Csproj(new[] { "Shop.Domain" }));
        Write(repo, "src/Shop.Application/Class1.cs", "namespace Shop.Application;\npublic sealed class Marker { }\n");
        Write(repo, "src/Shop.Infrastructure.Acme/Shop.Infrastructure.Acme.csproj", Csproj(new[] { "Shop.Domain", "Shop.Application" }));
        Write(repo, "src/Shop.Infrastructure.Acme/DependencyInjection.cs",
            "using Microsoft.Extensions.Configuration;\nusing Microsoft.Extensions.DependencyInjection;\n\nnamespace Shop.Infrastructure.Acme;\n\npublic static class DependencyInjection\n{\n    public static IServiceCollection AddAcmeAdapter(this IServiceCollection services, IConfiguration configuration, bool registerBackgroundWorkers = true)\n    {\n        return services;\n    }\n}\n");
        Write(repo, "src/Shop.Infrastructure/Shop.Infrastructure.csproj", Csproj(new[] { "Shop.Domain", "Shop.Application", "Shop.Infrastructure.Acme" }));
        Write(repo, "src/Shop.Infrastructure/Prices/AcmePriceSource.cs",
            "using Shop.Domain.Ports;\n\nnamespace Shop.Infrastructure.Prices;\n\ninternal sealed class AcmePriceSource : IPriceSource\n{\n    public decimal Get(int id) => id;\n}\n");
        Write(repo, "src/Shop.Infrastructure/DependencyInjection.cs",
            "using Microsoft.Extensions.Configuration;\nusing Microsoft.Extensions.DependencyInjection;\nusing Shop.Infrastructure.Acme;\nusing Shop.Infrastructure.Prices;\n\nnamespace Shop.Infrastructure;\n\npublic static class DependencyInjection\n{\n    public static IServiceCollection AddShopInfrastructure(this IServiceCollection services, IConfiguration configuration)\n    {\n        return services.AddAcmeAdapter(configuration);\n    }\n}\n");
        Write(repo, "src/Shop.Api/Shop.Api.csproj", Csproj(new[] { "Shop.Infrastructure" }));
        Write(repo, "src/Shop.Api/Program.cs", "using Shop.Infrastructure;\n\nvar services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();\nservices.AddShopInfrastructure(null!);\n");
        Write(repo, ".net-arch/project.yml", $"schema: 2\nblueprint: 1.0.0\nlayout: v3\nstandards:\n- {standards}\n");
        if (profile != null)
            Write(repo, ".net-arch/profile.yml", profile);
        return repo;
    }

    private static OperationResult Run(string name, string repo, bool apply, params (string Key, string Value)[] values)
    {
        var map = new Dictionary<string, string> { ["path"] = repo };
        foreach (var (key, value) in values)
            map[key] = value;
        return OperationRegistry.Execute(OperationRegistry.Find(name)!, map, apply);
    }

    [Fact]
    public void Move_code_plans_first_then_moves_the_namespace_and_every_using()
    {
        var repo = Repo();
        var plan = Run("move_code", repo, apply: false, ("from", "Shop.Infrastructure"), ("to", "Shop.Infrastructure.Acme"), ("files", "Prices"));

        Assert.True(plan.Ok, plan.Error);
        Assert.Contains(plan.Plan!, c => c.Action == "move" && c.Path == "src/Shop.Infrastructure.Acme/Prices/AcmePriceSource.cs");
        Assert.True(File.Exists(Path.Combine(repo, "src/Shop.Infrastructure/Prices/AcmePriceSource.cs")));

        var applied = Run("move_code", repo, apply: true, ("from", "Shop.Infrastructure"), ("to", "Shop.Infrastructure.Acme"), ("files", "Prices"));

        Assert.True(applied.Ok, applied.Error);
        Assert.False(File.Exists(Path.Combine(repo, "src/Shop.Infrastructure/Prices/AcmePriceSource.cs")));
        var moved = File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.Acme/Prices/AcmePriceSource.cs"));
        Assert.Contains("namespace Shop.Infrastructure.Acme.Prices;", moved, StringComparison.Ordinal);
        // nothing in the old namespace is left, and the file that used it no longer names it
        var di = File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure/DependencyInjection.cs"));
        Assert.DoesNotContain("using Shop.Infrastructure.Prices;", di, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_registration_moves_only_the_statements_that_name_an_adapter_type()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Infrastructure.Acme/Prices/AcmePriceSource.cs",
            "using Shop.Domain.Ports;\n\nnamespace Shop.Infrastructure.Acme.Prices;\n\npublic sealed class AcmePriceSource : IPriceSource\n{\n    public decimal Get(int id) => id;\n}\n");
        Write(repo, "src/Shop.Infrastructure/DependencyInjection.cs",
            "using Microsoft.Extensions.Configuration;\nusing Microsoft.Extensions.DependencyInjection;\nusing Shop.Infrastructure.Acme.Prices;\n\nnamespace Shop.Infrastructure;\n\npublic static class DependencyInjection\n{\n    public static IServiceCollection AddShopInfrastructure(this IServiceCollection services, IConfiguration configuration)\n    {\n        // shared\n        services.AddSingleton(\"x; {not a statement}\");\n\n        // acme\n        services.AddScoped<IPriceSource, AcmePriceSource>();\n        if (configuration[\"a\"] != null)\n        {\n            services.AddScoped<AcmePriceSource>();\n        }\n\n        return services;\n    }\n}\n");

        var plan = Run("split_registration", repo, apply: false, ("adapter", "Acme"));
        Assert.True(plan.Ok, plan.Error);
        Assert.Contains("DependencyInjection.cs", File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure/DependencyInjection.cs")) is { } ? "DependencyInjection.cs" : "");
        Assert.Contains("AddScoped<IPriceSource, AcmePriceSource>", File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure/DependencyInjection.cs")), StringComparison.Ordinal);

        var applied = Run("split_registration", repo, apply: true, ("adapter", "Acme"));
        Assert.True(applied.Ok, applied.Error);
        Assert.True(applied.Plan!.Count == 2, applied.Text);
        var shared = File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure/DependencyInjection.cs"));
        var adapter = File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.Acme/DependencyInjection.cs"));
        Assert.False(shared.Replace("using Shop.Infrastructure.Acme.Prices;", string.Empty).Contains("AcmePriceSource", StringComparison.Ordinal), shared);
        Assert.Contains("// shared", shared, StringComparison.Ordinal);
        Assert.Contains("return services;", shared, StringComparison.Ordinal);
        Assert.Contains("AddScoped<IPriceSource, AcmePriceSource>", adapter, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<AcmePriceSource>();", adapter, StringComparison.Ordinal);
        Assert.True(adapter.IndexOf("AcmePriceSource>();", StringComparison.Ordinal) < adapter.LastIndexOf("return services;", StringComparison.Ordinal));
    }

    [Fact]
    public void Describe_module_lists_the_permissions_of_the_modules_own_catalog()
    {
        var repo = Repo();
        Write(repo, ".corevia/mcp/shop-api-permissions.yaml",
            "permissions:\n  - key: Shop.Order.Create\n    status: active\n    action: write\n    authorizationPolicy: ShopServiceAccess\n  - key: Shop.Order.GetById\n    status: active\n    action: read\n    authorizationPolicy: AllowAnonymous\n  - key: Shop.Token.Raw\n    status: inventory-only\n    action: read\n    authorizationPolicy: none\n");

        var result = Run("describe_module", repo, apply: true);

        Assert.True(result.Ok, result.Error);
        var yaml = File.ReadAllText(Path.Combine(repo, "module.yaml"));
        Assert.Contains("- key: Shop.Order.Create\n    access: write\n    policy: ShopServiceAccess", yaml, StringComparison.Ordinal);
        Assert.Contains("- key: Shop.Order.GetById\n    access: read\n    policy: AllowAnonymous", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Shop.Token.Raw", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Outbound_http_in_the_shared_infrastructure_is_reported_and_leaves_with_the_adapter_that_add_adapter_creates()
    {
        var repo = Repo(profile: "name: acme\nversion: 1.0.0\nadapter_package: \"Acme.{Module}.Adapters.{System}\"\n");
        Write(repo, "src/Shop.Domain/Ports/IBillingClient.cs", "namespace Shop.Domain.Ports;\npublic interface IBillingClient { Task<int> Count(); }\n");
        Write(repo, "src/Shop.Infrastructure/ServiceClients/BillingClient.cs",
            "using Shop.Domain.Ports;\n\nnamespace Shop.Infrastructure.ServiceClients;\n\ninternal sealed class BillingClient(HttpClient http) : IBillingClient\n{\n    public Task<int> Count() => Task.FromResult(0);\n}\n");
        Write(repo, "src/Shop.Infrastructure/DependencyInjection.cs",
            "using Microsoft.Extensions.Configuration;\nusing Microsoft.Extensions.DependencyInjection;\nusing Shop.Domain.Ports;\nusing Shop.Infrastructure.ServiceClients;\n\nnamespace Shop.Infrastructure;\n\npublic static class DependencyInjection\n{\n    public static IServiceCollection AddShopInfrastructure(this IServiceCollection services, IConfiguration configuration)\n    {\n        services.AddHttpClient<IBillingClient, BillingClient>();\n        return services;\n    }\n}\n");

        var report = DoctorRunner.Run(repo);
        var finding = Assert.Single(report.Findings, f => f.Id == "DA-AD09");
        Assert.Contains("src/Shop.Infrastructure/ServiceClients/BillingClient.cs", finding.Details!);
        Assert.Contains("src/Shop.Infrastructure/DependencyInjection.cs", finding.Details!);

        var plan = Run("add_adapter", repo, apply: false, ("system", "BillingRest"));
        Assert.True(plan.Ok, plan.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, "src/Shop.Infrastructure.BillingRest")));
        var applied = Run("add_adapter", repo, apply: true, ("system", "BillingRest"));
        Assert.True(applied.Ok, applied.Error);
        var csproj = File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.BillingRest/Shop.Infrastructure.BillingRest.csproj"));
        Assert.Contains("<PackageId>Acme.Shop.Adapters.BillingRest</PackageId>", csproj, StringComparison.Ordinal);
        Assert.Contains("AdapterVersion", csproj, StringComparison.Ordinal);
        Assert.Contains("Shop.Domain.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Shop.Application", csproj, StringComparison.Ordinal);
        Assert.Contains("AddBillingRestAdapter", File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.BillingRest/DependencyInjection.cs")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repo, "src/Shop.Infrastructure.BillingRest/settings.schema.json")));
        Assert.Empty(Run("add_adapter", repo, apply: true, ("system", "BillingRest")).Plan!);

        Assert.True(Run("move_code", repo, apply: true, ("from", "Shop.Infrastructure"), ("to", "Shop.Infrastructure.BillingRest"), ("files", "ServiceClients")).Ok);
        Assert.True(Run("split_registration", repo, apply: true, ("adapter", "BillingRest")).Ok);

        var after = DoctorRunner.Run(repo);
        Assert.DoesNotContain(after.Findings, f => f.Id == "DA-AD09");
        Assert.Contains("AddHttpClient<IBillingClient, BillingClient>", File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.BillingRest/DependencyInjection.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Move_code_refuses_an_unknown_project_and_a_missing_file()
    {
        var repo = Repo();

        Assert.False(Run("move_code", repo, apply: false, ("from", "Shop.Nope"), ("to", "Shop.Domain"), ("files", "x")).Ok);
        Assert.False(Run("move_code", repo, apply: false, ("from", "Shop.Infrastructure"), ("to", "Shop.Infrastructure.Acme"), ("files", "Missing.cs")).Ok);
    }

    [Fact]
    public void Adapter_rules_find_the_coupling_and_name_the_fix()
    {
        var repo = Repo();

        var report = DoctorRunner.Run(repo);

        Assert.Contains(report.Findings, f => f.Id == "DA-AD01" && f.Details!.Contains("Shop.Infrastructure.Acme -> Shop.Application"));
        Assert.Contains(report.Findings, f => f.Id == "DA-AD02" && f.Details!.Contains("Shop.Infrastructure -> Shop.Infrastructure.Acme"));
        Assert.Contains(report.Findings, f => f.Id == "DA-AD05");
        Assert.DoesNotContain(report.Findings, f => f.Id == "DA-AD04");
    }

    [Fact]
    public void A_flat_key_an_adapter_reads_is_outside_every_schema()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Infrastructure.Acme/Prices/Reader.cs",
            "using Microsoft.Extensions.Configuration;\n\nnamespace Shop.Infrastructure.Acme.Prices;\n\ninternal sealed class Reader(IConfiguration configuration)\n{\n    public string? Flat() => configuration[\"ACME_BASE_URL\"];\n    public string? Area() => configuration[\"Acme:BaseUrl\"];\n}\n");

        var finding = Assert.Single(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-AD07");

        Assert.Contains("Shop.Infrastructure.Acme: ACME_BASE_URL", finding.Details!);
        Assert.Contains("Shop.Infrastructure.Acme: Acme:BaseUrl", finding.Details!);
    }

    [Fact]
    public void The_core_projects_an_adapter_depends_on_become_packages_at_the_module_version()
    {
        var repo = Repo(profile: "name: acme\nversion: 1.0.0\nadapter_package: \"Acme.{Module}.Adapters.{System}\"\nmodule_package: \"Acme.{Module}.{Layer}\"\n");
        Write(repo, "src/Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <PropertyGroup>\n    <Version>2.3.0</Version>\n  </PropertyGroup>\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Shop.Infrastructure\\Shop.Infrastructure.csproj\" />\n  </ItemGroup>\n</Project>\n");

        Assert.Contains(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-AD08" && f.Details!.Contains("Shop.Domain"));
        var applied = Run("fix", repo, apply: true, ("rules", "DA-AD08"));

        Assert.True(applied.Ok, applied.Error);
        var csproj = File.ReadAllText(Path.Combine(repo, "src/Shop.Domain/Shop.Domain.csproj"));
        Assert.Contains("<PackageId>Acme.Shop.Domain</PackageId>", csproj, StringComparison.Ordinal);
        Assert.Contains("<Version>2.3.0</Version>", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain(DoctorRunner.Run(repo).Findings, f => f.Id == "DA-AD08");
    }

    [Fact]
    public void Adapter_rules_are_off_unless_the_standard_is_on()
    {
        var repo = Repo(standards: "abp");

        Assert.DoesNotContain(DoctorRunner.Run(repo).Findings, f => f.Id.StartsWith("DA-AD", StringComparison.Ordinal));
    }

    [Fact]
    public void Fix_removes_an_unused_adapter_reference_and_keeps_a_used_one_as_manual()
    {
        var repo = Repo();

        var unused = Run("fix", repo, apply: true, ("rules", "DA-AD01"));

        Assert.True(unused.Ok, unused.Error);
        Assert.DoesNotContain("Shop.Application", File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.Acme/Shop.Infrastructure.Acme.csproj")), StringComparison.Ordinal);

        // the shared Infrastructure project still uses the adapter's namespace: the reference stays and the fix says why
        var used = Run("fix", repo, apply: true, ("rules", "DA-AD02"));

        Assert.Contains("is used", used.Text, StringComparison.Ordinal);
        Assert.Contains("Shop.Infrastructure.Acme", File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure/Shop.Infrastructure.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Package_id_comes_from_the_profile_pattern_never_from_the_tool()
    {
        var without = Repo();
        var manual = Run("fix", without, apply: false, ("rules", "DA-AD05"));
        Assert.Contains("adapter_package", manual.Text, StringComparison.Ordinal);
        Directory.Delete(without, recursive: true);

        var repo = Repo(profile: "name: acme\nversion: 1.0.0\nadapter_package: \"Acme.{Module}.Adapters.{System}\"\n");
        var applied = Run("fix", repo, apply: true, ("rules", "DA-AD05"));

        Assert.True(applied.Ok, applied.Error);
        var csproj = File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.Acme/Shop.Infrastructure.Acme.csproj"));
        Assert.Contains("<PackageId>Acme.Shop.Adapters.Acme</PackageId>", csproj, StringComparison.Ordinal);
        Assert.Contains("<IsPackable>true</IsPackable>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_composition_creates_the_project_and_points_the_call_sites_at_it()
    {
        var repo = Repo();

        var applied = Run("add_composition", repo, apply: true, ("adapters", "Acme"));

        Assert.True(applied.Ok, applied.Error);
        var code = File.ReadAllText(Path.Combine(repo, "src/Shop.Composition/DependencyInjection.cs"));
        Assert.Contains("AddShopComposition(this IServiceCollection services, IConfiguration configuration, bool registerBackgroundWorkers = true)", code, StringComparison.Ordinal);
        Assert.Contains("services.AddShopInfrastructure(configuration);", code, StringComparison.Ordinal);
        Assert.Contains("AddAcmeAdapter(configuration, registerBackgroundWorkers)", code, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(repo, "src/Shop.Api/Program.cs"));
        Assert.Contains("AddShopComposition", program, StringComparison.Ordinal);
        Assert.Contains("using Shop.Composition;", program, StringComparison.Ordinal);
        Assert.Contains("Shop.Composition.csproj", File.ReadAllText(Path.Combine(repo, "src/Shop.Api/Shop.Api.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_module_lists_ports_adapters_and_what_they_implement_and_read()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Infrastructure.Acme/Prices/AcmePriceSource.cs",
            "using Microsoft.Extensions.Configuration;\nusing Shop.Domain.Ports;\n\nnamespace Shop.Infrastructure.Acme.Prices;\n\ninternal sealed class AcmePriceSource(IConfiguration configuration) : IPriceSource\n{\n    public decimal Get(int id) => decimal.Parse(configuration[\"Acme:BaseUrl\"]!);\n}\n");

        var plan = Run("describe_module", repo, apply: false);
        Assert.True(plan.Ok, plan.Error);
        Assert.False(File.Exists(Path.Combine(repo, "module.yaml")));

        var applied = Run("describe_module", repo, apply: true);

        Assert.True(applied.Ok, applied.Error);
        var yaml = File.ReadAllText(Path.Combine(repo, "module.yaml"));
        Assert.Contains("module: Shop", yaml, StringComparison.Ordinal);
        Assert.Contains("- name: IPriceSource", yaml, StringComparison.Ordinal);
        Assert.Contains("system: Acme", yaml, StringComparison.Ordinal);
        Assert.Contains("- AddAcmeAdapter", yaml, StringComparison.Ordinal);
        Assert.Contains("- IPriceSource", yaml, StringComparison.Ordinal);
        Assert.Contains("- Acme:BaseUrl", yaml, StringComparison.Ordinal);
        Assert.Contains("backgroundWorkers: true", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_settings_writes_a_closed_schema_and_marks_secrets_write_only()
    {
        var repo = Repo();
        Write(repo, "src/Shop.Infrastructure.Acme/Options/AcmeOptions.cs",
            "namespace Shop.Infrastructure.Acme;\n\npublic sealed class AcmeOptions\n{\n    public const string SectionName = \"Acme\";\n    public string BaseUrl { get; set; } = string.Empty;\n    public int TimeoutSeconds { get; set; } = 30;\n    public string ApiKey { get; set; } = string.Empty;\n    public AcmeRetry Retry { get; set; } = new();\n}\n\npublic sealed class AcmeRetry\n{\n    public int Attempts { get; set; } = 3;\n}\n");

        var applied = Run("describe_settings", repo, apply: true);

        Assert.True(applied.Ok, applied.Error);
        var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "src/Shop.Infrastructure.Acme/settings.schema.json"))).RootElement;
        Assert.False(json.GetProperty("additionalProperties").GetBoolean());
        var acme = json.GetProperty("properties").GetProperty("Acme");
        Assert.Equal("integer", acme.GetProperty("properties").GetProperty("TimeoutSeconds").GetProperty("type").GetString());
        Assert.Equal(30, acme.GetProperty("properties").GetProperty("TimeoutSeconds").GetProperty("default").GetInt32());
        Assert.True(acme.GetProperty("properties").GetProperty("ApiKey").GetProperty("writeOnly").GetBoolean());
        Assert.Equal("integer", acme.GetProperty("properties").GetProperty("Retry").GetProperty("properties").GetProperty("Attempts").GetProperty("type").GetString());
        // nested options classes are part of their owner, not reported as unresolved
        Assert.DoesNotContain("AcmeRetry", applied.Text, StringComparison.Ordinal);
    }
}
