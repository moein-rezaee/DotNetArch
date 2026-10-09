using DotNetArch.Core.Doctor;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

public sealed class DoctorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-doctor-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public DoctorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Csproj(params string[] references) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>" + string.Concat(references.Select(r => $"<ProjectReference Include=\"{r}\" />")) + "</ItemGroup></Project>";

    [Fact]
    public void Generated_v2_solution_has_no_blocking_findings()
    {
        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner)))
        {
            SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite"));
            CrudScaffolder.Generate(DotNetArch.Core.Config.ConfigManager.Load(Path.Combine(_root, "Acme"))!, "Product");
        }

        var report = DoctorRunner.Run(Path.Combine(_root, "Acme"));

        Assert.Equal("v2", report.Layout);
        Assert.DoesNotContain(report.Findings, f => f.Severity == DoctorSeverity.Error);
    }

    [Fact]
    public void Upward_reference_and_missing_layers_are_errors()
    {
        Write("Shop.Domain/Shop.Domain.csproj", Csproj("../Shop.Infrastructure/Shop.Infrastructure.csproj"));
        Write("Shop.Infrastructure/Shop.Infrastructure.csproj", Csproj());

        var report = DoctorRunner.Run(_root);

        Assert.Contains(report.Findings, f => f.Id == "DA-S02" && f.Severity == DoctorSeverity.Error);
        var direction = Assert.Single(report.Findings, f => f.Id == "DA-S03");
        Assert.Contains("Shop.Domain -> Shop.Infrastructure", direction.Details!);
        Assert.False(report.IsHealthy());
    }

    [Fact]
    public void Secrets_in_appsettings_are_found_but_placeholders_are_not()
    {
        Write("Shop.Api/Shop.Api.csproj", Csproj());
        Write("Shop.Api/appsettings.json", """{ "Db": { "Password": "hunter2hunter2" }, "Jwt": { "Secret": "<set-me>" }, "X": { "ConnectionString": "Server=a;Password=abc123;" } }""");

        var finding = Assert.Single(DoctorRunner.Run(_root).Findings, f => f.Id == "DA-C05");

        Assert.Equal(DoctorSeverity.Error, finding.Severity);
        Assert.Contains(finding.Details!, d => d.EndsWith("Db:Password", StringComparison.Ordinal));
        Assert.Contains(finding.Details!, d => d.EndsWith("X:ConnectionString", StringComparison.Ordinal));
        Assert.DoesNotContain(finding.Details!, d => d.EndsWith("Jwt:Secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Profile_rules_apply_from_profile_yml_and_the_tool_itself_knows_no_organisation()
    {
        Write("Shop.Api/Dockerfile", "# syntax=docker/dockerfile:1.7\nFROM a AS build\nFROM b\n");
        Write("Shop.Application/Shop.Application.csproj", Csproj("../shared/Common/Common.csproj"));
        Write(".net-arch/profile.yml", """
            name: acme
            version: 1.0.0
            accepted_layouts: [flat]
            rules:
              - id: AC-01
                kind: require-files
                severity: error
                message: Governance metadata missing.
                files: [.acme/repo.yaml, .acme/ci/]
              - id: AC-02
                kind: dockerfile-forbid-line
                severity: error
                message: Dockerfile pins a syntax frontend.
                prefix: "# syntax="
              - id: AC-03
                kind: forbid-project-reference
                severity: error
                message: Projects reference shared sources.
                pattern: '(^|/)shared/'
            """);

        var report = DoctorRunner.Run(_root);

        Assert.Equal("acme 1.0.0", report.Profile);
        Assert.Contains(report.Findings, f => f.Id == "AC-01" && f.Severity == DoctorSeverity.Error);
        Assert.Contains(report.Findings, f => f.Id == "AC-02");
        Assert.Contains(report.Findings, f => f.Id == "AC-03");
        Assert.DoesNotContain(report.Findings, f => f.Id == "DA-S06");
        Assert.DoesNotContain(report.Findings, f => f.Id.StartsWith("DA-V", StringComparison.Ordinal));
        Assert.Contains(report.Findings, f => f.Id == "DA-D04");
    }

    [Fact]
    public void Missing_shared_profile_is_a_note_not_a_failure()
    {
        Write("Shop.Api/Shop.Api.csproj", Csproj());
        Write(".net-arch/profile.yml", "name: acme\nversion: 1.0.0\nsource: ../acme-standards/profile.yml\n");

        var report = DoctorRunner.Run(_root);

        Assert.Equal("none", report.Profile);
        Assert.Contains(report.Notes, n => n.Contains("Profile source not found", StringComparison.Ordinal));
    }

    [Fact]
    public void Rules_yml_overrides_severity_and_accepts_exceptions_with_a_reason()
    {
        Write("Shop.Api/Shop.Api.csproj", Csproj());
        Write("Shop.Api/Startup.cs", "class S { void M() { builder.AllowAnyOrigin(); Console.WriteLine(\"x\"); } }");
        Write(".net-arch/rules.yml", """
            severity: { DA-K01: error, DA-B03: off }
            exceptions:
              - rule: DA-K04
                reason: Public read-only API behind the gateway
            """);

        var report = DoctorRunner.Run(_root);

        Assert.Contains(report.Findings, f => f.Id == "DA-K01" && f.Severity == DoctorSeverity.Error);
        Assert.DoesNotContain(report.Findings, f => f.Id == "DA-B03");
        Assert.DoesNotContain(report.Findings, f => f.Id == "DA-K04");
        var accepted = Assert.Single(report.Accepted);
        Assert.Equal("DA-K04", accepted.Id);
        Assert.Contains("gateway", accepted.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Docs_and_test_sections_report_missing_documents_empty_tests_and_coverage()
    {
        Write("Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Swashbuckle.AspNetCore\" Version=\"6.0.0\" /></ItemGroup></Project>");
        Write("Shop.Api.Tests/Shop.Api.Tests.csproj", Csproj());
        Write("Shop.Api.Tests/Empty.cs", "class Nothing {}");
        Write("docs/specs/openspec.yaml", "a: [unclosed");
        Write("TestResults/coverage.cobertura.xml", "<coverage line-rate=\"0.40\"></coverage>");
        Write(".net-arch/rules.yml", "thresholds: { coverage_line: 70 }\n");

        var report = DoctorRunner.Run(_root);

        Assert.Contains(report.Findings, f => f.Id == "DA-M06");
        Assert.Contains(report.Findings, f => f.Id == "DA-M07");
        Assert.Contains(report.Findings, f => f.Id == "DA-M08" && f.Severity == DoctorSeverity.Error);
        Assert.Contains(report.Findings, f => f.Id == "DA-M09");
        Assert.Contains(report.Findings, f => f.Id == "DA-T01");
        Assert.Contains(report.Findings, f => f.Id == "DA-T02" && f.Severity == DoctorSeverity.Warning && f.Message.Contains("40.0%", StringComparison.Ordinal));
    }

    [Fact]
    public void Kit_implementation_package_in_application_is_an_error_and_code_rules_are_warnings()
    {
        Write("Shop.Application/Shop.Application.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Acme.Kit.Cache.Providers.Redis\" Version=\"1.0.0\" /></ItemGroup></Project>");
        Write("Shop.Application/Ports/IRepo.cs", "public interface IRepo { System.Linq.IQueryable<int> All(); }");
        Write("Shop.Api/Shop.Api.csproj", Csproj());
        Write("Shop.Api/Startup.cs", "class S { void M() { Console.WriteLine(\"x\"); var v = DoAsync().Result; } }");

        var report = DoctorRunner.Run(_root);

        Assert.Contains(report.Findings, f => f.Id == "DA-K06" && f.Severity == DoctorSeverity.Error);
        Assert.Contains(report.Findings, f => f.Id == "DA-K03");
        Assert.Contains(report.Findings, f => f.Id == "DA-K01");
        Assert.Contains(report.Findings, f => f.Id == "DA-K02");
    }

    [Fact]
    public void Json_report_has_summary_and_text_report_lists_findings()
    {
        Write("Shop.Api/Shop.Api.csproj", Csproj());
        var report = DoctorRunner.Run(_root);

        var json = DoctorFormatter.ToJson(report);
        var text = DoctorFormatter.ToText(report);

        Assert.Contains("\"checksRun\"", json);
        Assert.Contains("\"severity\": \"error\"", json);
        Assert.Contains("DA-S02", text);
    }

    [Fact]
    public void Missing_folder_is_a_usage_error() =>
        Assert.Throws<ArgumentException>(() => DoctorRunner.Run(Path.Combine(_root, "nope")));
}
