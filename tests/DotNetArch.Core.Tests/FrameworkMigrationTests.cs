using DotNetArch.Core.Operations;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>migrate_framework (R-K10): the .NET version of a repository moves in one plan, the target is an input, and a second run changes nothing.</summary>
public sealed class FrameworkMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-framework-" + Guid.NewGuid().ToString("N"));

    public FrameworkMigrationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Write(string repo, string relative, string content)
    {
        var path = Path.Combine(repo, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string Repo()
    {
        var repo = Path.Combine(_root, "shop");
        Write(repo, "src/Shop.Api/Shop.Api.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.Extensions.Http\" Version=\"9.0.1\" />\n    <PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.2\" />\n    <PackageReference Include=\"Serilog\" Version=\"9.0.0\" />\n  </ItemGroup>\n</Project>\n");
        Write(repo, "src/Shop.Domain/Shop.Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>\n  </PropertyGroup>\n</Project>\n");
        Write(repo, "Directory.Packages.props", "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"Microsoft.EntityFrameworkCore\" Version=\"9.0.0\" />\n    <PackageVersion Include=\"Microsoft.AspNetCore.Mvc.Testing\" Version=\"8.0.4\" />\n    <PackageVersion Include=\"xunit\" Version=\"2.9.0\" />\n  </ItemGroup>\n</Project>\n");
        Write(repo, "global.json", "{\n  \"sdk\": {\n    \"version\": \"9.0.306\",\n    \"rollForward\": \"latestFeature\"\n  }\n}\n");
        Write(repo, "src/Shop.Api/Dockerfile", "FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build\nCOPY --from=build /app/bin/Release/net9.0/ /app\nFROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine\nFROM ${DOCKER_REGISTRY}/dotnet/runtime:9.0 AS tool\n");
        Write(repo, ".gitlab-ci.yml", "build:\n  image: mcr.microsoft.com/dotnet/sdk:9.0.306\n");
        Write(repo, "README.md", "Runs on net9.0 today.\n");
        return repo;
    }

    private static OperationResult Run(string repo, string target, bool apply) =>
        OperationRegistry.Execute(OperationRegistry.Find("migrate_framework")!, new Dictionary<string, string> { ["path"] = repo, ["target"] = target }, apply);

    private static string Read(string repo, string relative) => File.ReadAllText(Path.Combine(repo, relative));

    [Fact]
    public void A_plan_lists_the_changes_and_writes_nothing()
    {
        var repo = Repo();

        var result = Run(repo, "10", apply: false);

        Assert.True(result.Ok);
        Assert.False(result.Applied);
        Assert.Contains(result.Plan!, c => c.Path == "src/Shop.Api/Shop.Api.csproj");
        Assert.Contains("net9.0", Read(repo, "src/Shop.Api/Shop.Api.csproj"));
    }

    [Fact]
    public void Apply_moves_frameworks_packages_sdk_pin_and_image_tags_to_the_target()
    {
        var repo = Repo();

        var result = Run(repo, "10", apply: true);

        Assert.True(result.Applied);
        var api = Read(repo, "src/Shop.Api/Shop.Api.csproj");
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", api);
        Assert.Contains("Include=\"Microsoft.Extensions.Http\" Version=\"10.0.0\"", api);
        Assert.Contains("Include=\"Serilog\" Version=\"9.0.0\"", api);
        Assert.Contains("Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.2\"", api);
        Assert.Contains("<TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>", Read(repo, "src/Shop.Domain/Shop.Domain.csproj"));
        var props = Read(repo, "Directory.Packages.props");
        Assert.Contains("Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.0\"", props);
        Assert.Contains("Include=\"Microsoft.AspNetCore.Mvc.Testing\" Version=\"10.0.0\"", props);
        Assert.Contains("Include=\"xunit\" Version=\"2.9.0\"", props);
        Assert.Contains("\"version\": \"10.0.100\"", Read(repo, "global.json"));
        Assert.Contains("\"rollForward\": \"latestFeature\"", Read(repo, "global.json"));
        var docker = Read(repo, "src/Shop.Api/Dockerfile");
        Assert.Contains("sdk:10.0 AS build", docker);
        Assert.Contains("/app/bin/Release/net10.0/", docker);
        Assert.Contains("aspnet:10.0-alpine", docker);
        Assert.Contains("FROM ${DOCKER_REGISTRY}/dotnet/runtime:10.0 AS tool", docker);
        Assert.Contains("sdk:10.0\n", Read(repo, ".gitlab-ci.yml"));
        Assert.Equal("Runs on net9.0 today.\n", Read(repo, "README.md"));
    }

    [Fact]
    public void Third_party_packages_tied_to_the_runtime_are_listed_not_changed()
    {
        var repo = Repo();

        var result = Run(repo, "10", apply: false);

        var manual = ((IEnumerable<string>)result.Data!.GetType().GetProperty("manual")!.GetValue(result.Data)!).ToList();
        Assert.Contains(manual, m => m.Contains("Npgsql.EntityFrameworkCore.PostgreSQL") && m.Contains("net10.0"));
        Assert.Contains(manual, m => m.Contains("Install the .NET 10 SDK"));
    }

    [Fact]
    public void A_second_run_and_a_lower_target_change_nothing()
    {
        var repo = Repo();
        Run(repo, "10", apply: true);

        var again = Run(repo, "10", apply: true);
        var lower = Run(repo, "9", apply: true);

        Assert.Empty(again.Plan!);
        Assert.Empty(lower.Plan!);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", Read(repo, "src/Shop.Api/Shop.Api.csproj"));
    }

    [Fact]
    public void The_target_is_an_input_so_an_older_step_works_too()
    {
        var repo = Repo();

        Run(repo, "9", apply: true);

        Assert.Contains("<TargetFrameworks>net9.0;netstandard2.0</TargetFrameworks>", Read(repo, "src/Shop.Domain/Shop.Domain.csproj"));
        Assert.Contains("Include=\"Microsoft.AspNetCore.Mvc.Testing\" Version=\"9.0.0\"", Read(repo, "Directory.Packages.props"));
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", Read(repo, "src/Shop.Api/Shop.Api.csproj"));
    }

    [Fact]
    public void A_target_below_six_is_refused()
    {
        var result = Run(Repo(), "5", apply: false);

        Assert.False(result.Ok);
        Assert.Contains("6 or higher", result.Error);
    }

    private static string FakeDotnetRoot(string folder)
    {
        Write(folder, "packs/Microsoft.NETCore.App.Ref/10.0.3/data/PackageOverrides.txt", "System.Text.Json|10.0.0\n");
        Write(folder, "packs/Microsoft.AspNetCore.App.Ref/10.0.3/data/PackageOverrides.txt", "Microsoft.Extensions.Configuration.Binder|10.0.0\nMicrosoft.Extensions.Http|10.0.0\n");
        return folder;
    }

    [Fact]
    public void From_net10_references_the_framework_now_provides_are_removed_in_web_projects_and_their_dependents()
    {
        var repo = Path.Combine(_root, "web");
        var refs = "  <ItemGroup>\n    <PackageReference Include=\"Microsoft.Extensions.Configuration.Binder\" />\n    <PackageReference Include=\"System.Text.Json\" />\n    <PackageReference Include=\"Serilog\" />\n  </ItemGroup>\n";
        Write(repo, "src/App.Api/App.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n" + refs + "</Project>\n");
        Write(repo, "test/App.Api.Tests/App.Api.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <ProjectReference Include=\"..\\..\\src\\App.Api\\App.Api.csproj\" />\n    <PackageReference Include=\"Microsoft.Extensions.Http\" />\n  </ItemGroup>\n</Project>\n");
        Write(repo, "src/App.Core/App.Core.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n" + refs + "</Project>\n");
        Write(repo, "Directory.Packages.props", "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"Microsoft.Extensions.Configuration.Binder\" Version=\"9.0.0\" />\n    <PackageVersion Include=\"Microsoft.Extensions.Http\" Version=\"9.0.0\" />\n    <PackageVersion Include=\"System.Text.Json\" Version=\"9.0.0\" />\n    <PackageVersion Include=\"Serilog\" Version=\"4.0.0\" />\n  </ItemGroup>\n</Project>\n");

        var result = OperationRegistry.Execute(OperationRegistry.Find("migrate_framework")!,
            new Dictionary<string, string> { ["path"] = repo, ["target"] = "10", ["dotnet_root"] = FakeDotnetRoot(Path.Combine(_root, "sdk")) }, apply: true);

        Assert.True(result.Ok);
        var api = Read(repo, "src/App.Api/App.Api.csproj");
        Assert.DoesNotContain("Configuration.Binder", api);
        Assert.DoesNotContain("System.Text.Json", api);
        Assert.Contains("Include=\"Serilog\"", api);
        var tests = Read(repo, "test/App.Api.Tests/App.Api.Tests.csproj");
        Assert.DoesNotContain("Microsoft.Extensions.Http", tests);
        var library = Read(repo, "src/App.Core/App.Core.csproj");
        Assert.Contains("Configuration.Binder", library);
        Assert.DoesNotContain("System.Text.Json", library);
    }

    [Fact]
    public void Without_the_target_sdk_nothing_is_removed_and_the_plan_says_so()
    {
        var repo = Path.Combine(_root, "nosdk");
        Write(repo, "src/App.Api/App.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.Extensions.Configuration.Binder\" />\n  </ItemGroup>\n</Project>\n");
        var empty = Path.Combine(_root, "empty-sdk");
        Directory.CreateDirectory(empty);

        var result = OperationRegistry.Execute(OperationRegistry.Find("migrate_framework")!,
            new Dictionary<string, string> { ["path"] = repo, ["target"] = "10", ["dotnet_root"] = empty }, apply: true);

        Assert.Contains("Configuration.Binder", Read(repo, "src/App.Api/App.Api.csproj"));
        var manual = ((IEnumerable<string>)result.Data!.GetType().GetProperty("manual")!.GetValue(result.Data)!).ToList();
        Assert.Contains(manual, m => m.Contains("NU1510"));
    }

    [Fact]
    public void Version_overrides_move_too_and_pinned_packages_get_exactly_the_given_version()
    {
        var repo = Path.Combine(_root, "pins");
        Write(repo, "src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.Extensions.Options\" VersionOverride=\"9.0.1\" />\n    <PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"9.0.2\" />\n  </ItemGroup>\n</Project>\n");

        var result = OperationRegistry.Execute(OperationRegistry.Find("migrate_framework")!,
            new Dictionary<string, string> { ["path"] = repo, ["target"] = "10", ["packages"] = "Npgsql.EntityFrameworkCore.PostgreSQL=10.0.0" }, apply: true);

        Assert.True(result.Ok);
        var project = Read(repo, "src/App/App.csproj");
        Assert.Contains("Include=\"Microsoft.Extensions.Options\" VersionOverride=\"10.0.0\"", project);
        Assert.Contains("Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"10.0.0\"", project);
    }

    [Fact]
    public void A_malformed_package_pin_is_refused()
    {
        var result = OperationRegistry.Execute(OperationRegistry.Find("migrate_framework")!,
            new Dictionary<string, string> { ["path"] = Repo(), ["target"] = "10", ["packages"] = "Npgsql" }, apply: false);

        Assert.False(result.Ok);
        Assert.Contains("Id=Version", result.Error);
    }
}
