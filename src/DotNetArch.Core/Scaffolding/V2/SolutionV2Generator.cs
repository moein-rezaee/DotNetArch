using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Creates a layout-v2 microservice: <c>src/</c> layers (Domain, Application, Infrastructure, Api) wired per layer.</summary>
public static class SolutionV2Generator
{
    private const string Layers = "Domain|Application|Infrastructure|Api";

    internal static readonly string[] TestLayers = { "Domain", "Application", "Infrastructure", "Api" };

    public static SolutionConfig Generate(SolutionRequest request)
    {
        var app = Identifier.RequireSolutionName(request.Name);
        Identifier.RequirePath(request.OutputPath, "output path");
        var solutionDir = Path.Combine(request.OutputPath, app);
        Directory.CreateDirectory(solutionDir);

        var tfm = $"net{PackageVersionResolver.ResolveTargetMajor(SolutionTooling.ResolveTargetFramework())}.0";
        var major = PackageVersionResolver.ResolveTargetMajor(tfm);

        bool Run(string command) => ToolHost.RunCommand(command, solutionDir);

        var gitInstalled = SolutionTooling.IsGitInstalled();
        var gitInitialized = false;
        var ops = request.Ops ?? new OpsOptions();
        var wantGit = !ops.NoGit && ToolHost.AskYesNo("Initialize git repository?", true);
        if (wantGit && !gitInstalled)
            ToolHost.Error("Git is not installed.");

        var wantReadme = ToolHost.AskYesNo("Create README.md?", true);

        var provider = string.IsNullOrWhiteSpace(request.ProviderOverride)
            ? DatabaseProviderSelector.ChooseV2()
            : request.ProviderOverride!;
        if (!DatabaseProviders.IsSupported(provider))
            throw new ArgumentException($"Database provider '{provider}' is not supported by layout v2. Use one of: {string.Join(", ", DatabaseProviders.Supported)}.");
        provider = DatabaseProviders.Normalize(provider);

        var port = StablePort(app);
        var tokens = BuildTokens(app, tfm, major, provider, port);
        var withTests = !ops.NoTests;
        var writer = new FileWriter(solutionDir);

        foreach (var template in TemplateRenderer.List("V2/solution"))
        {
            if (!withTests && template.StartsWith("V2/solution/tests/", StringComparison.Ordinal))
                continue;

            writer.Write(OutputPath(template, app), TemplateRenderer.RenderTemplate(template, tokens));
        }

        if (wantReadme)
            writer.Write("README.md", TemplateRenderer.RenderTemplate("V2/docs/README.md.tpl", tokens));

        ToolHost.Success($"Generated {writer.Created.Count} files for layout v2.");

        if (wantGit && gitInstalled)
        {
            gitInitialized = Run("git init");
            if (gitInitialized)
                Run("git branch -M main");
        }

        Run($"dotnet new sln -n {app} --force");
        foreach (var layer in Layers.Split('|'))
            Run($"dotnet sln add src/{app}.{layer}/{app}.{layer}.csproj");
        if (withTests)
        {
            foreach (var layer in TestLayers)
                Run($"dotnet sln add tests/{app}.{layer}.Tests/{app}.{layer}.Tests.csproj");
        }

        var config = new SolutionConfig
        {
            SolutionName = app,
            SolutionPath = solutionDir,
            StartupProject = $"{app}.Api",
            DatabaseProvider = provider,
            ApiStyle = request.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "controller",
            ApiPort = port.ToString(),
            TargetFramework = tfm,
            Layout = SolutionConfig.V2Layout,
            DockerRegistry = ops.DockerRegistry?.Trim() ?? string.Empty,
            NuGetSource = ops.NuGetSource?.Trim() ?? string.Empty,
            NuGetSourceName = ops.NuGetSourceName?.Trim() ?? string.Empty,
        };
        ConfigManager.Save(solutionDir, config);
        PathState.Save(solutionDir);

        if (!string.IsNullOrWhiteSpace(ops.GitRemote) || !string.IsNullOrWhiteSpace(ops.GitProvider) || !string.IsNullOrWhiteSpace(ops.GitHost))
            OpsGenerator.SetupGit(config, ops.GitRemote, ops.GitHost, ops.GitProvider);

        OpsGenerator.AddNuGetConfig(config);
        if (!ops.NoDocker && ToolHost.AskYesNo("Add Docker support?", true))
            OpsGenerator.AddDocker(config);

        var ci = OpsGenerator.ResolveCiProvider(config, ops.Ci);
        if (ci is not null)
            OpsGenerator.AddCi(config, ci);

        if (ops.Mcp)
            McpV2Generator.Add(config);

        if (gitInstalled && gitInitialized)
        {
            Run("git add .");
            Run("git commit -m \"init\"");
        }

        ToolHost.Blank();
        ToolHost.Success("Solution created successfully!");
        ToolHost.Info($"Navigate to the '{app}' directory and run 'dotnet build'.");
        return config;
    }

    internal static Dictionary<string, string> BuildTokens(string app, string tfm, int major, string provider, int port)
    {
        var databaseName = app.Replace('.', '_').ToLowerInvariant();
        var example = DatabaseProviders.ExampleConnectionString(provider, databaseName);
        return new Dictionary<string, string>
        {
            ["App"] = app,
            ["Tfm"] = tfm,
            ["SdkVersion"] = PackageCatalog.SdkVersion(major),
            ["Port"] = port.ToString(),
            ["Provider"] = provider,
            ["MediatRVersion"] = PackageCatalog.MediatR,
            ["FluentValidationVersion"] = PackageCatalog.FluentValidation,
            ["SwashbuckleVersion"] = PackageCatalog.Swashbuckle,
            ["EfCoreVersion"] = PackageCatalog.EfCore(major),
            ["AspNetVersion"] = PackageCatalog.AspNet(major),
            ["ExtDependencyInjectionVersion"] = PackageCatalog.ExtDependencyInjection(major),
            ["ExtOptionsConfigurationVersion"] = PackageCatalog.ExtOptionsConfiguration(major),
            ["ExtConfigurationJsonVersion"] = PackageCatalog.ExtConfigurationJson(major),
            ["ExtHostingAbstractionsVersion"] = PackageCatalog.ExtHostingAbstractions(major),
            // Infrastructure tests run against in-memory SQLite; avoid a duplicate entry when SQLite is the app's own provider.
            ["TestSqlitePackageLine"] = provider == DatabaseProviders.Sqlite
                ? string.Empty
                : $"    <PackageVersion Include=\"Microsoft.EntityFrameworkCore.Sqlite\" Version=\"{PackageCatalog.EfCore(major)}\" />\n",
            ["EfProviderPackageId"] = DatabaseProviders.PackageId(provider),
            ["EfProviderPackageVersion"] = PackageCatalog.EfProviderVersion(provider, major),
            ["UseProviderStatement"] = DatabaseProviders.UseStatement(provider),
            // SQLite tests get a private file per factory; other engines never connect in the generated API tests (health/error only).
            ["TestConnectionExpression"] = provider == DatabaseProviders.Sqlite ? "$\"Data Source={_databaseFile}\"" : "\"" + example + "\"",
            ["DesignTimeConnectionString"] = example,
            ["ExampleConnectionString"] = example,
            ["DevConnectionString"] = example,
        };
    }

    /// <summary>Maps <c>V2/solution/src/Domain/Common/Entity.cs.tpl</c> to <c>src/{App}.Domain/Common/Entity.cs</c>.</summary>
    internal static string OutputPath(string template, string app)
    {
        var relative = template["V2/solution/".Length..];
        relative = relative.EndsWith(".tpl", StringComparison.Ordinal) ? relative[..^4] : relative;

        if (relative.StartsWith("tests/", StringComparison.Ordinal))
        {
            var testParts = relative.Split('/');
            var project = testParts[1]; // e.g. Domain.Tests
            testParts[1] = $"{app}.{project}";
            if (testParts.Length == 3 && testParts[2].Equals($"{project}.csproj", StringComparison.Ordinal))
                testParts[2] = $"{app}.{project}.csproj";
            return string.Join('/', testParts);
        }

        if (!relative.StartsWith("src/", StringComparison.Ordinal))
            return relative;

        var parts = relative.Split('/');
        var layer = parts[1];
        parts[1] = $"{app}.{layer}";
        if (parts.Length == 3 && parts[2].Equals($"{layer}.csproj", StringComparison.Ordinal))
            parts[2] = $"{app}.{layer}.csproj";
        return string.Join('/', parts);
    }

    /// <summary>Deterministic dev port in 5100-5999 so a solution keeps the same port across machines.</summary>
    internal static int StablePort(string name)
    {
        var hash = 17;
        foreach (var ch in name)
            hash = unchecked(hash * 31 + ch);
        return 5100 + Math.Abs(hash % 900);
    }
}
