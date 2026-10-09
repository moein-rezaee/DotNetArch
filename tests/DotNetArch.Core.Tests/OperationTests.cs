using System.Security.Cryptography;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.NetArch;
using DotNetArch.Core.Operations;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

public sealed class OperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-ops2-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public OperationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string GenerateSolution()
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite"));
        CrudScaffolder.Generate(DotNetArch.Core.Config.ConfigManager.Load(Path.Combine(_root, "Acme"))!, "Product");
        return Path.Combine(_root, "Acme");
    }

    private static Dictionary<string, string> Fingerprint(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.net-arch{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(root, f), f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    private static OperationResult Run(string name, string root, bool apply, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string> { ["path"] = root };
        foreach (var (key, value) in extra)
            values[key] = value;
        return OperationRegistry.Execute(OperationRegistry.Find(name)!, values, apply);
    }

    [Fact]
    public void Registry_defines_every_operation_once_with_a_read_only_or_mutating_kind()
    {
        var names = OperationRegistry.All.Select(o => o.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(new[] { "adopt", "doctor", "fix", "new_solution", "new_crud", "add_layer", "add_tests", "spec_add", "graph" }, n => Assert.Contains(n, names));
        Assert.Equal(OperationKind.ReadOnly, OperationRegistry.Find("doctor")!.Kind);
        Assert.All(new[] { "adopt", "fix" }, n => Assert.Equal(OperationKind.Mutating, OperationRegistry.Find(n)!.Kind));
    }

    [Fact]
    public void Adopt_plans_first_then_writes_only_inside_net_arch_and_changes_no_source_file()
    {
        var solution = GenerateSolution();
        var before = Fingerprint(solution);

        var plan = Run("adopt", solution, apply: false);
        Assert.False(plan.Applied);
        Assert.False(Directory.Exists(Path.Combine(solution, ".net-arch")));
        Assert.All(plan.Plan!, c => Assert.StartsWith(".net-arch/", c.Path, StringComparison.Ordinal));

        var applied = Run("adopt", solution, apply: true);
        Assert.True(applied.Applied);
        Assert.Equal(before, Fingerprint(solution));
        var state = NetArchStore.LoadState(solution)!;
        Assert.Equal(NetArchFiles.CurrentSchema, state.Schema);
        Assert.Contains("domain", state.Layers);
        Assert.Contains("Products", state.Entities.Keys);

        var again = Run("adopt", solution, apply: true);
        Assert.Empty(again.Plan!);
    }

    [Fact]
    public void Project_is_independent_of_the_tool_folder()
    {
        var solution = GenerateSolution();
        Run("adopt", solution, apply: true);
        var withState = DoctorRunner.Run(solution);

        Directory.Delete(Path.Combine(solution, ".net-arch"), recursive: true);
        var without = DoctorRunner.Run(solution);

        Assert.Equal(withState.Errors, without.Errors);
        Assert.Equal(withState.Findings.Select(f => f.Id), without.Findings.Select(f => f.Id));
    }

    [Fact]
    public void Adopt_keeps_rules_and_profile_and_references_a_shared_profile_by_relative_path()
    {
        var solution = GenerateSolution();
        var shared = Path.Combine(_root, "standards", "acme.profile.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(shared)!);
        File.WriteAllText(shared, "name: acme\nversion: 2.0.0\nrules: []\n");

        Run("adopt", solution, apply: true, ("profile", shared));
        var profileFile = File.ReadAllText(Path.Combine(solution, ".net-arch", "profile.yml"));
        File.WriteAllText(Path.Combine(solution, ".net-arch", "rules.yml"), "severity: { DA-K01: error }\n");
        Run("adopt", solution, apply: true);

        Assert.Contains("source: ../standards/acme.profile.yml", profileFile, StringComparison.Ordinal);
        Assert.Contains("DA-K01: error", File.ReadAllText(Path.Combine(solution, ".net-arch", "rules.yml")), StringComparison.Ordinal);
        Assert.Equal("acme 2.0.0", DoctorRunner.Run(solution).Profile);
    }

    [Fact]
    public void Fix_creates_only_missing_hygiene_files_and_never_touches_code()
    {
        var repo = Path.Combine(_root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Shop.Api"));
        File.WriteAllText(Path.Combine(repo, "Shop.Api", "Shop.Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(Path.Combine(repo, "Shop.Api", "Dockerfile"), "FROM a AS build\nFROM b\nUSER app\n");
        File.WriteAllText(Path.Combine(repo, "Shop.Api", "Program.cs"), "class P { void M() { Console.WriteLine(1); } }");
        File.WriteAllText(Path.Combine(repo, ".gitignore"), "bin/\n");
        var before = Fingerprint(repo);

        var plan = Run("fix", repo, apply: false);
        Assert.Equal(before, Fingerprint(repo));
        Assert.Contains(plan.Plan!, c => c.Path == ".editorconfig" && c.RuleId == "DA-B05");
        Assert.Contains(plan.Plan!, c => c.Path == ".dockerignore");
        Assert.Contains(plan.Plan!, c => c.Path == ".gitignore" && c.Action == "modify");

        Run("fix", repo, apply: true);

        Assert.True(File.Exists(Path.Combine(repo, ".editorconfig")));
        Assert.True(File.Exists(Path.Combine(repo, ".dockerignore")));
        Assert.True(File.Exists(Path.Combine(repo, "global.json")));
        Assert.Contains(".env", File.ReadAllText(Path.Combine(repo, ".gitignore")), StringComparison.Ordinal);
        Assert.Equal(before["Shop.Api/Program.cs".Replace('/', Path.DirectorySeparatorChar)], Fingerprint(repo)["Shop.Api/Program.cs".Replace('/', Path.DirectorySeparatorChar)]);
        Assert.Empty(Run("fix", repo, apply: false).Plan!);
    }

    [Fact]
    public void Missing_required_value_and_unknown_folder_are_usage_errors()
    {
        var doctor = OperationRegistry.Find("doctor")!;
        var missing = Run("doctor", Path.Combine(_root, "nope"), apply: false);
        Assert.False(missing.Ok);
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("Folder not found", missing.Error, StringComparison.Ordinal);
        Assert.True(OperationRegistry.ToJson(doctor, Run("doctor", _root, apply: false)).Contains("\"operation\": \"doctor\"", StringComparison.Ordinal));
    }

    private string Structural()
    {
        var repo = Path.Combine(_root, "structural");
        void Write(string relative, string content)
        {
            var path = Path.Combine(repo, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        Write("Shop.sln", "");
        Write("Directory.Build.props", "<Project>\n  <PropertyGroup>\n    <NuGetAudit>false</NuGetAudit>\n  </PropertyGroup>\n</Project>\n");
        Write("Shop.Domain/Shop.Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>\n</Project>\n");
        Write("Shop.Application/Shop.Application.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <PackageReference Include=\"MediatR\" Version=\"12.2.0\" />\n    <PackageReference Include=\"Serilog\" Version=\"3.0.0\" />\n  </ItemGroup>\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Domain\\Shop.Domain.csproj\" /></ItemGroup>\n</Project>\n");
        Write("Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <ItemGroup>\n    <PackageReference Include=\"MediatR\" Version=\"12.2.0\" />\n    <PackageReference Include=\"Serilog\" Version=\"4.0.0\" PrivateAssets=\"all\" />\n  </ItemGroup>\n</Project>\n");
        Write("Shop.Application.Tests/Shop.Application.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"xunit\" Version=\"2.9.3\" />\n    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.12.0\" />\n    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.8.2\" />\n  </ItemGroup>\n</Project>\n");
        return repo;
    }

    [Fact]
    public void Structural_fixers_are_opt_in_and_listed_as_manual_by_default()
    {
        var repo = Structural();

        var plan = Run("fix", repo, apply: false);

        Assert.DoesNotContain(plan.Plan!, c => c.RuleId is "DA-B03" or "DA-B07" or "DA-S04");
        Assert.Contains("opt-in: --rules=DA-B03", plan.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Central_packages_keep_every_resolved_version_through_version_override()
    {
        var repo = Structural();

        Run("fix", repo, apply: true, ("rules", "DA-B03"));

        var props = File.ReadAllText(Path.Combine(repo, "Directory.Packages.props"));
        Assert.Contains("ManagePackageVersionsCentrally", props, StringComparison.Ordinal);
        Assert.Contains("<PackageVersion Include=\"MediatR\" Version=\"12.2.0\" />", props, StringComparison.Ordinal);
        var application = File.ReadAllText(Path.Combine(repo, "Shop.Application", "Shop.Application.csproj"));
        var api = File.ReadAllText(Path.Combine(repo, "Shop.Api", "Shop.Api.csproj"));
        Assert.DoesNotContain("Version=", application.Replace("VersionOverride", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("PrivateAssets=\"all\"", api, StringComparison.Ordinal);
        Assert.Contains("VersionOverride=\"3.0.0\"", application + api, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionOverride=\"4.0.0\"", application + api, StringComparison.Ordinal);
        Assert.Contains("<PackageVersion Include=\"Serilog\" Version=\"4.0.0\" />", props, StringComparison.Ordinal);
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-B03")).Plan!);
    }

    [Fact]
    public void Strict_warnings_extend_the_existing_props_file_once()
    {
        var repo = Structural();

        Run("fix", repo, apply: true, ("rules", "DA-B07"));

        var props = File.ReadAllText(Path.Combine(repo, "Directory.Build.props"));
        Assert.Contains("<NuGetAudit>false</NuGetAudit>", props, StringComparison.Ordinal);
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", props, StringComparison.Ordinal);
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-B07")).Plan!);
    }

    [Fact]
    public void Domain_test_project_is_created_registered_and_never_created_twice()
    {
        var repo = Structural();
        var runner = new FakeProcessRunner();

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), runner)))
            Run("fix", repo, apply: true, ("rules", "DA-S04"));

        var csproj = File.ReadAllText(Path.Combine(repo, "Shop.Domain.Tests", "Shop.Domain.Tests.csproj"));
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.Contains("Include=\"xunit\" Version=\"2.9.3\"", csproj, StringComparison.Ordinal);
        Assert.Contains("Shop.Domain.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("Assembly.Load(\"Shop.Domain\")", File.ReadAllText(Path.Combine(repo, "Shop.Domain.Tests", "DomainLayerTests.cs")), StringComparison.Ordinal);
        Assert.Contains(runner.Calls, c => c.Arguments.Contains("sln") && c.Arguments.Any(a => a.Contains("Shop.Domain.Tests", StringComparison.Ordinal)));
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-S04")).Plan!);
    }

    [Fact]
    public void Domain_tests_created_together_with_central_packages_carry_no_inline_versions()
    {
        var repo = Structural();

        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner())))
            Run("fix", repo, apply: true, ("rules", "DA-B03,DA-S04"));

        var csproj = File.ReadAllText(Path.Combine(repo, "Shop.Domain.Tests", "Shop.Domain.Tests.csproj"));
        Assert.Contains("<PackageReference Include=\"xunit\" />", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageVersion Include=\"xunit\" Version=\"2.9.3\" />", File.ReadAllText(Path.Combine(repo, "Directory.Packages.props")), StringComparison.Ordinal);
    }

    private string Flat()
    {
        var repo = Path.Combine(_root, "flat");
        void Write(string relative, string content)
        {
            var path = Path.Combine(repo, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        Write("Shop.sln", "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Api\", \"Shop.Api\\Shop.Api.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Api.Tests\", \"Shop.Api.Tests\\Shop.Api.Tests.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\nEndProject\n");
        Write("Shop.Domain/Shop.Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        Write("Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\">\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Shop.Domain\\Shop.Domain.csproj\" />\n    <Content Include=\"..\\shared\\data.json\" Link=\"data.json\" />\n    <None Include=\"appsettings.json\" />\n    <PackageReference Include=\"MediatR\" />\n  </ItemGroup>\n</Project>\n");
        Write("Shop.Api/Dockerfile", "# docker build -f Shop.Api/Dockerfile .\nCOPY Shop.Api/Shop.Api.csproj Shop.Api/\nCOPY Shop.Domain/Shop.Domain.csproj Shop.Domain/\nRUN dotnet publish Shop.Api/Shop.Api.csproj -o /app\n");
        Write("Shop.Api.Tests/Shop.Api.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup><ProjectReference Include=\"..\\Shop.Api\\Shop.Api.csproj\" /></ItemGroup>\n</Project>\n");
        Write("docker-compose.yml", "services:\n  api:\n    build:\n      dockerfile: Shop.Api/Dockerfile\n    volumes:\n      - ./Shop.Api/appsettings.json:/app/appsettings.json\n");
        Write("Shop.Api.Tests/PathTests.cs", "namespace Shop.Api.Tests;\npublic class PathTests\n{\n    string A(string root) => Path.Combine(root, \"Shop.Api\", \"Shop.Api.csproj\");\n    string B = \"Shop.Api/Dockerfile\";\n    System.Type T = typeof(Shop.Api.Marker);\n}\n");
        Write(".corevia/operations/tests.values.yaml", "tests:\n  - name: api\n    path: Shop.Api.Tests\n  - note: Shop.Api.Tests is mentioned in prose\n");
        Write("docs/evidence/old.md", "built with Shop.Api/Dockerfile\n");
        Write("docs/specs/contracts.md", "Controllers live in Shop.Api/Controllers.\n");
        return repo;
    }

    [Fact]
    public void Layout_move_is_opt_in_and_moves_projects_and_rewrites_every_path_that_points_at_them()
    {
        var repo = Flat();

        var plan = Run("fix", repo, apply: false, ("rules", "DA-S06"));
        Assert.Contains(plan.Plan!, c => c.Action == "move" && c.Path == "src/Shop.Api");
        Assert.Contains(plan.Plan!, c => c.Action == "move" && c.Path == "tests/Shop.Api.Tests");
        Assert.True(Directory.Exists(Path.Combine(repo, "Shop.Api")));

        Run("fix", repo, apply: true, ("rules", "DA-S06"));

        Assert.False(Directory.Exists(Path.Combine(repo, "Shop.Api")));
        var api = File.ReadAllText(Path.Combine(repo, "src", "Shop.Api", "Shop.Api.csproj"));
        Assert.Contains("Include=\"..\\Shop.Domain\\Shop.Domain.csproj\"", api, StringComparison.Ordinal);
        Assert.Contains("Include=\"..\\..\\shared\\data.json\"", api, StringComparison.Ordinal);
        Assert.Contains("Include=\"appsettings.json\"", api, StringComparison.Ordinal);
        var tests = File.ReadAllText(Path.Combine(repo, "tests", "Shop.Api.Tests", "Shop.Api.Tests.csproj"));
        Assert.Contains("Include=\"..\\..\\src\\Shop.Api\\Shop.Api.csproj\"", tests, StringComparison.Ordinal);
        var solution = File.ReadAllText(Path.Combine(repo, "Shop.sln"));
        Assert.Contains("\"src\\Shop.Api\\Shop.Api.csproj\"", solution, StringComparison.Ordinal);
        Assert.Contains("\"tests\\Shop.Api.Tests\\Shop.Api.Tests.csproj\"", solution, StringComparison.Ordinal);
        var dockerfile = File.ReadAllText(Path.Combine(repo, "src", "Shop.Api", "Dockerfile"));
        Assert.Contains("-f src/Shop.Api/Dockerfile", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY src/Shop.Domain/Shop.Domain.csproj src/Shop.Domain/", dockerfile, StringComparison.Ordinal);
        Assert.Contains("dockerfile: src/Shop.Api/Dockerfile", File.ReadAllText(Path.Combine(repo, "docker-compose.yml")), StringComparison.Ordinal);
        Assert.Contains("./src/Shop.Api/appsettings.json", File.ReadAllText(Path.Combine(repo, "docker-compose.yml")), StringComparison.Ordinal);
        Assert.Contains("Shop.Api/Controllers", File.ReadAllText(Path.Combine(repo, "docs", "specs", "contracts.md")), StringComparison.Ordinal);
        Assert.Contains("src/Shop.Api/Controllers", File.ReadAllText(Path.Combine(repo, "docs", "specs", "contracts.md")), StringComparison.Ordinal);
        Assert.Equal("built with Shop.Api/Dockerfile\n", File.ReadAllText(Path.Combine(repo, "docs", "evidence", "old.md")));
        var code = File.ReadAllText(Path.Combine(repo, "tests", "Shop.Api.Tests", "PathTests.cs"));
        Assert.Contains("Path.Combine(root, \"src\", \"Shop.Api\", \"Shop.Api.csproj\")", code, StringComparison.Ordinal);
        Assert.Contains("\"src/Shop.Api/Dockerfile\"", code, StringComparison.Ordinal);
        Assert.Contains("typeof(Shop.Api.Marker)", code, StringComparison.Ordinal);
        var values = File.ReadAllText(Path.Combine(repo, ".corevia", "operations", "tests.values.yaml"));
        Assert.Contains("path: tests/Shop.Api.Tests", values, StringComparison.Ordinal);
        Assert.Contains("note: Shop.Api.Tests is mentioned in prose", values, StringComparison.Ordinal);
        Assert.Equal("v2", DoctorRunner.Run(repo).Layout);
        Assert.Empty(Run("fix", repo, apply: false, ("rules", "DA-S06")).Plan!);
    }
}
