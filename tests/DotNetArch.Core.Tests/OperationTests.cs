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
        Assert.Equal(new[] { "adopt", "doctor", "fix" }, OperationRegistry.All.Select(o => o.Name).OrderBy(n => n, StringComparer.Ordinal));
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
}
