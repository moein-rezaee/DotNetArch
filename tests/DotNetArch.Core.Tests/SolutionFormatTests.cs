using DotNetArch.Core.Operations;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>migrate_solution_format: a classic .sln becomes a .slnx with the same projects and folders, references move, and a second run changes nothing.</summary>
public sealed class SolutionFormatTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-slnx-" + Guid.NewGuid().ToString("N"));

    public SolutionFormatTests() => Directory.CreateDirectory(_root);

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
        Write(repo, "Shop.sln",
            "Microsoft Visual Studio Solution File, Format Version 12.00\n"
            + "Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"src\", \"src\", \"{AAAAAAAA-0000-0000-0000-000000000001}\"\n"
            + "EndProject\n"
            + "Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"Solution Items\", \"Solution Items\", \"{AAAAAAAA-0000-0000-0000-000000000002}\"\n"
            + "\tProjectSection(SolutionItems) = preProject\n\t\tREADME.md = README.md\n\tEndProjectSection\n"
            + "EndProject\n"
            + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Api\", \"src\\Shop.Api\\Shop.Api.csproj\", \"{BBBBBBBB-0000-0000-0000-000000000001}\"\n"
            + "EndProject\n"
            + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Tests\", \"test\\Shop.Tests\\Shop.Tests.csproj\", \"{BBBBBBBB-0000-0000-0000-000000000002}\"\n"
            + "EndProject\n"
            + "Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"api\", \"api\", \"{AAAAAAAA-0000-0000-0000-000000000003}\"\nEndProject\n"
            + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Shop.Web\", \"src\\Shop.Web\\Shop.Web.csproj\", \"{BBBBBBBB-0000-0000-0000-000000000003}\"\nEndProject\n"
            + "Global\n\tGlobalSection(NestedProjects) = preSolution\n\t\t{BBBBBBBB-0000-0000-0000-000000000001} = {AAAAAAAA-0000-0000-0000-000000000001}\n\t\t{AAAAAAAA-0000-0000-0000-000000000003} = {AAAAAAAA-0000-0000-0000-000000000001}\n\t\t{BBBBBBBB-0000-0000-0000-000000000003} = {AAAAAAAA-0000-0000-0000-000000000003}\n\tEndGlobalSection\nEndGlobal\n");
        Write(repo, "src/Shop.Api/Shop.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        Write(repo, "test/Shop.Tests/Shop.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        Write(repo, "Dockerfile", "COPY *.sln ./\nRUN dotnet restore Shop.sln\n");
        Write(repo, "README.md", "Open Shop.sln in the IDE.\n");
        return repo;
    }

    private static OperationResult Run(string repo, bool apply) =>
        OperationRegistry.Execute(OperationRegistry.Find("migrate_solution_format")!, new Dictionary<string, string> { ["path"] = repo }, apply);

    [Fact]
    public void A_plan_lists_the_changes_and_writes_nothing()
    {
        var repo = Repo();

        var result = Run(repo, apply: false);

        Assert.True(result.Ok);
        Assert.Contains(result.Plan!, c => c.Path == "Shop.slnx");
        Assert.True(File.Exists(Path.Combine(repo, "Shop.sln")));
        Assert.False(File.Exists(Path.Combine(repo, "Shop.slnx")));
    }

    [Fact]
    public void Apply_keeps_projects_folders_and_items_and_removes_the_old_file()
    {
        var repo = Repo();

        Run(repo, apply: true);

        var slnx = File.ReadAllText(Path.Combine(repo, "Shop.slnx"));
        Assert.False(File.Exists(Path.Combine(repo, "Shop.sln")));
        Assert.Contains("<Folder Name=\"/src/\">", slnx);
        Assert.Contains("<Project Path=\"src/Shop.Api/Shop.Api.csproj\" />", slnx);
        Assert.Contains("<Project Path=\"test/Shop.Tests/Shop.Tests.csproj\" />", slnx);
        Assert.Contains("<File Path=\"README.md\" />", slnx);
        Assert.Contains("<Folder Name=\"/src/api/\">", slnx);
        Assert.Contains("<Project Path=\"src/Shop.Web/Shop.Web.csproj\" />", slnx);
        Assert.DoesNotContain("<Folder Name=\"/src/\">\n    <Folder", slnx);
        Assert.Contains("COPY *.slnx ./", File.ReadAllText(Path.Combine(repo, "Dockerfile")));
        Assert.Contains("Shop.slnx", File.ReadAllText(Path.Combine(repo, "Dockerfile")));
    }

    [Fact]
    public void A_second_run_changes_nothing()
    {
        var repo = Repo();
        Run(repo, apply: true);

        var again = Run(repo, apply: true);

        Assert.Empty(again.Plan!);
    }
}
