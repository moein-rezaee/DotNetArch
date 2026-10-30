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
