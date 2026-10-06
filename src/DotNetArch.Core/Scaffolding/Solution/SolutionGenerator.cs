using System.Text.Json;

namespace DotNetArch.Core.Scaffolding.Solution;

public sealed record SolutionRequest(
    string Name,
    string OutputPath,
    string StartupProject,
    string ApiStyle,
    string? ProviderOverride = null);

/// <summary>Creates a new legacy-layout solution (<c>new solution</c>).</summary>
public static class SolutionGenerator
{
    public static void Generate(SolutionRequest request)
    {
        var solutionName = Identifier.RequireSolutionName(request.Name);
        var startupProject = request.StartupProject;
        var solutionDir = Path.Combine(request.OutputPath, solutionName);
        Directory.CreateDirectory(solutionDir);

        bool Run(string command) => ToolHost.RunCommand(command, solutionDir);

        EnsureGlobalJson(solutionDir);
        var tfm = SolutionTooling.ResolveTargetFramework();

        var gitInstalled = SolutionTooling.IsGitInstalled();
        var gitInitialized = false;
        if (ToolHost.AskYesNo("Initialize git repository?", true))
        {
            EnsureDotnetGitIgnore(solutionDir);
            if (gitInstalled)
            {
                gitInitialized = Run("git init");
                if (gitInitialized)
                    Run("git branch -M main");
            }
            else
            {
                ToolHost.Error("Git is not installed.");
            }
        }

        if (ToolHost.AskYesNo("Create README.md?", true))
            EnsureReadmeTemplate(solutionDir, solutionName);

        Run($"dotnet new sln -n {solutionName} --force");
        Run($"dotnet new classlib -n {solutionName}.Core --force --framework {tfm}");
        Run($"dotnet new classlib -n {solutionName}.Application --force --framework {tfm}");
        Run($"dotnet new classlib -n {solutionName}.Infrastructure --force --framework {tfm}");
        Run($"dotnet new webapi -n {solutionName}.API --force --framework {tfm}");

        DeleteDefaultClass(solutionDir, $"{solutionName}.Core");
        DeleteDefaultClass(solutionDir, $"{solutionName}.Application");
        DeleteDefaultClass(solutionDir, $"{solutionName}.Infrastructure");

        Run($"dotnet sln add {solutionName}.Core/{solutionName}.Core.csproj");
        Run($"dotnet sln add {solutionName}.Application/{solutionName}.Application.csproj");
        Run($"dotnet sln add {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");
        Run($"dotnet sln add {solutionName}.API/{solutionName}.API.csproj");

        Run($"dotnet add {solutionName}.Application/{solutionName}.Application.csproj reference {solutionName}.Core/{solutionName}.Core.csproj");
        Run($"dotnet add {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj reference {solutionName}.Application/{solutionName}.Application.csproj");
        Run($"dotnet add {solutionName}.API/{solutionName}.API.csproj reference {solutionName}.Application/{solutionName}.Application.csproj");
        Run($"dotnet add {solutionName}.API/{solutionName}.API.csproj reference {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");

        var provider = string.IsNullOrWhiteSpace(request.ProviderOverride) ? DatabaseProviderSelector.Choose() : request.ProviderOverride!;
        var port = ReadApiPort(solutionDir, startupProject);
        var config = new SolutionConfig
        {
            SolutionName = solutionName,
            SolutionPath = solutionDir,
            StartupProject = startupProject,
            DatabaseProvider = provider,
            ApiStyle = request.ApiStyle,
            ApiPort = port,
            TargetFramework = tfm
        };
        ConfigManager.Save(solutionDir, config);
        PathState.Save(solutionDir);
        new ApplicationStep().Execute(config, string.Empty);
        new ProjectUpdateStep().Execute(config, string.Empty);

        if (ToolHost.AskYesNo("Add Docker support?", true))
            DockerSupport.CreateArtifacts(solutionDir, solutionName, startupProject, port);

        if (gitInstalled && gitInitialized)
        {
            Run("git add .");
            Run("git commit -m \"init\"");
        }

        ToolHost.Blank();
        ToolHost.Success("Solution created successfully!");
        ToolHost.Info($"Navigate to the '{solutionName}' directory and run 'dotnet build'.");
    }

    private static void EnsureGlobalJson(string basePath)
    {
        try
        {
            var path = Path.Combine(basePath, "global.json");
            if (File.Exists(path)) return;
            var content = "{\n  \"sdk\": {\n    \"version\": \"8.0.100\",\n    \"rollForward\": \"latestMajor\",\n    \"allowPrerelease\": false\n  }\n}";
            File.WriteAllText(path, content);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ReadApiPort(string basePath, string startupProject)
    {
        var lsPath = Path.Combine(basePath, startupProject, "Properties", "launchSettings.json");
        if (!File.Exists(lsPath))
            return "5000";
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(lsPath));
            if (doc.RootElement.TryGetProperty("profiles", out var profiles))
            {
                foreach (var prof in profiles.EnumerateObject())
                {
                    if (prof.Value.TryGetProperty("applicationUrl", out var urlEl))
                    {
                        var url = urlEl.GetString() ?? string.Empty;
                        var http = url.Split(';').FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
                        if (http != null && Uri.TryCreate(http, UriKind.Absolute, out var uri))
                            return uri.Port.ToString();
                    }
                }
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        return "5000";
    }

    private static void EnsureDotnetGitIgnore(string basePath)
    {
        var gitignorePath = Path.Combine(basePath, ".gitignore");
        if (File.Exists(gitignorePath))
            return;

        if (!ToolHost.RunCommand("dotnet new gitignore", basePath))
        {
            var lines = new[]
            {
                "# Build Folders", "bin/", "obj/", "publish/", "",
                "# User-specific files", "*.rsuser", "*.suo", "*.user", "*.userosscache", "*.sln.docstates", "",
                "# Visual Studio", ".vs/"
            };
            File.WriteAllLines(gitignorePath, lines);
        }
        ToolHost.Success(".gitignore file added.");
    }

    private static void EnsureReadmeTemplate(string basePath, string solutionName)
    {
        var readmePath = Path.Combine(basePath, "README.md");
        if (File.Exists(readmePath))
            return;

        var content =
            $"# {solutionName}\n\n" +
            "A brief description of your project.\n\n" +
            "## Table of Contents\n\n" +
            "- [Features](#features)\n" +
            "- [Getting Started](#getting-started)\n" +
            "  - [Prerequisites](#prerequisites)\n" +
            "  - [Installation](#installation)\n" +
            "  - [Usage](#usage)\n" +
            "- [Contributing](#contributing)\n" +
            "- [License](#license)\n" +
            "- [Acknowledgements](#acknowledgements)\n\n" +
            "## Features\n\n" +
            "- Describe the features of your project.\n\n" +
            "## Getting Started\n\n" +
            "### Prerequisites\n\n" +
            "- List prerequisites here.\n\n" +
            "### Installation\n\n" +
            "```bash\n# Installation steps\n```\n\n" +
            "### Usage\n\n" +
            "```bash\n# Usage example\n```\n\n" +
            "## Contributing\n\n" +
            "Describe how to contribute.\n\n" +
            "## License\n\n" +
            "Specify the license.\n\n" +
            "## Acknowledgements\n\n" +
            "- List acknowledgements.\n";
        File.WriteAllText(readmePath, content);
        ToolHost.Success("README.md file added.");
    }

    private static void DeleteDefaultClass(string solutionDir, string projectName)
    {
        var filePath = Path.Combine(solutionDir, projectName, "Class1.cs");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            ToolHost.Info("Deleted default class", Path.Combine(projectName, "Class1.cs"));
        }
    }
}
