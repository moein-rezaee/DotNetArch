using System.Text.RegularExpressions;
using DotNetArch.Core.Templating;
using DotNetArch.Core.Scaffolding.V2;

namespace DotNetArch.Core.Scaffolding.Ops;

/// <summary>Adds Docker, CI, git hosting and registry configuration to a v2 solution (<c>docker add</c>, <c>ci add</c>, <c>git setup</c>).</summary>
public static class OpsGenerator
{
    private static readonly string[] CiChoices = { "GitHub Actions", "GitLab CI", "Azure Pipelines", "Bitbucket Pipelines", "None" };

    // --- docker ------------------------------------------------------------------------------------------------------
    public static void AddDocker(SolutionConfig config)
    {
        RequireV2(config);
        var tokens = Tokens(config);
        var writer = new FileWriter(config.SolutionPath);

        tokens["RestoreInstruction"] = string.IsNullOrWhiteSpace(config.NuGetSource)
            ? $"RUN dotnet restore src/{config.SolutionName}.Api/{config.SolutionName}.Api.csproj"
            : $"RUN --mount=type=secret,id=nuget_config,target=/root/.nuget/NuGet/NuGet.Config dotnet restore src/{config.SolutionName}.Api/{config.SolutionName}.Api.csproj";
        tokens["NuGetConfigCopy"] = string.IsNullOrWhiteSpace(config.NuGetSource) ? string.Empty : "NuGet.config ";

        var compose = config.DatabaseProvider switch
        {
            DatabaseProviders.Postgres => "postgres",
            DatabaseProviders.SqlServer => "sqlserver",
            _ => "sqlite"
        };
        tokens["ComposeEnvLines"] = config.DatabaseProvider switch
        {
            DatabaseProviders.Postgres => "POSTGRES_PASSWORD=<choose-a-password>",
            DatabaseProviders.SqlServer => "SQLSERVER_PASSWORD=<choose-a-strong-password>",
            _ => "# SQLite needs no variables."
        };

        writer.Write($"src/{config.SolutionName}.Api/Dockerfile", TemplateRenderer.RenderTemplate("V2/ops/Dockerfile.tpl", tokens));
        writer.Write(".dockerignore", TemplateRenderer.Load("V2/ops/dockerignore.tpl"));
        writer.Write("docker-compose.yml", TemplateRenderer.RenderTemplate($"V2/ops/compose.{compose}.yml.tpl", tokens));
        writer.Write(".env.example", TemplateRenderer.RenderTemplate("V2/ops/root.env.example.tpl", tokens));

        config.DockerImage = tokens["ImageName"];
        config.DockerContainer = tokens["ContainerName"];
        ConfigManager.Save(config.SolutionPath, config);
        ToolHost.Success($"Docker support added ({writer.Created.Count} files).");
    }

    // --- nuget -------------------------------------------------------------------------------------------------------
    public static void AddNuGetConfig(SolutionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.NuGetSource))
            return;

        if (!Uri.TryCreate(config.NuGetSource, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException($"'{config.NuGetSource}' is not a valid NuGet source URL.");
        if (uri.UserInfo.Length > 0)
            throw new ArgumentException("Do not put credentials in the NuGet source URL; supply them through CI secrets or the environment.");

        if (string.IsNullOrWhiteSpace(config.NuGetSourceName))
            config.NuGetSourceName = "private";

        new FileWriter(config.SolutionPath).Write("NuGet.config", TemplateRenderer.RenderTemplate("V2/ops/NuGet.config.tpl", Tokens(config)));
        ConfigManager.Save(config.SolutionPath, config);
        ToolHost.Success("NuGet.config added (credentials are expected from the environment).");
    }

    // --- git ---------------------------------------------------------------------------------------------------------
    public static bool SetupGit(SolutionConfig config, string? remote, string? host, string? provider)
    {
        if (!string.IsNullOrWhiteSpace(provider) && !GitHosts.IsKnownProvider(provider))
        {
            ToolHost.Error($"Unknown git provider '{provider}'.", $"Use one of: {string.Join(", ", GitHosts.Providers)}.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(remote))
        {
            if (remote.StartsWith('-') || remote.Any(char.IsControl))
            {
                ToolHost.Error($"'{remote}' is not a valid git remote.");
                return false;
            }

            if (!Directory.Exists(Path.Combine(config.SolutionPath, ".git")) && !ToolHost.RunCommand("git init", config.SolutionPath))
                return false;

            var hasOrigin = ToolHost.RunCommand("git remote get-url origin", config.SolutionPath, print: false);
            var ok = ToolHost.RunCommand(hasOrigin ? $"git remote set-url origin {remote}" : $"git remote add origin {remote}", config.SolutionPath);
            if (!ok)
                return false;
        }

        provider = !string.IsNullOrWhiteSpace(provider) ? provider!.ToLowerInvariant() : GitHosts.Detect(remote) ?? config.GitProvider;
        if (!string.IsNullOrWhiteSpace(provider))
            config.GitProvider = provider;

        var hostValue = !string.IsNullOrWhiteSpace(host) ? host!.TrimEnd('/') : GitHosts.HostOf(remote);
        if (!string.IsNullOrWhiteSpace(hostValue) && !GitHosts.IsPublicHost(hostValue))
            config.GitHost = hostValue;

        ConfigManager.Save(config.SolutionPath, config);
        ToolHost.Success("Git settings saved.", string.IsNullOrWhiteSpace(config.GitProvider) ? null : $"provider: {config.GitProvider}");
        return true;
    }

    // --- ci ----------------------------------------------------------------------------------------------------------
    /// <summary>Picks the CI provider: explicit choice, else the configured git provider, else the origin remote, else ask (default none).</summary>
    public static string? ResolveCiProvider(SolutionConfig config, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && !requested.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (requested.Equals("none", StringComparison.OrdinalIgnoreCase))
                return null;
            if (!GitHosts.IsKnownProvider(requested))
                throw new ArgumentException($"Unknown CI provider '{requested}'. Use auto, none or one of: {string.Join(", ", GitHosts.Providers)}.");
            return requested.ToLowerInvariant();
        }

        if (GitHosts.IsKnownProvider(config.GitProvider))
            return config.GitProvider;

        var (ok, origin) = ToolHost.RunCommandCapture("git remote get-url origin", config.SolutionPath);
        if (ok && GitHosts.Detect(origin.Trim()) is { } detected)
            return detected;

        var choice = ToolHost.AskOption("Select CI provider", CiChoices, CiChoices.Length - 1);
        return choice switch
        {
            "GitHub Actions" => GitHosts.GitHub,
            "GitLab CI" => GitHosts.GitLab,
            "Azure Pipelines" => GitHosts.Azure,
            "Bitbucket Pipelines" => GitHosts.Bitbucket,
            _ => null
        };
    }

    public static bool AddCi(SolutionConfig config, string provider)
    {
        RequireV2(config);
        provider = provider.ToLowerInvariant();
        var tokens = Tokens(config);
        var withDocker = !string.IsNullOrWhiteSpace(config.DockerRegistry);
        var family = provider == GitHosts.Gitea ? GitHosts.GitHub : provider;

        tokens["RegistryHost"] = RegistryHost(config.DockerRegistry);
        tokens["NuGetEnv"] = NuGetEnv(config, family);
        tokens["NuGetExport"] = NuGetExport(config);
        var withKits = !string.IsNullOrWhiteSpace(config.NuGetSource);
        string Snippet(string name) => TemplateRenderer.RenderTemplate($"V2/ops/ci/{family}.{name}.yml.tpl", tokens).TrimEnd('\n') + "\n";

        tokens["DockerJob"] = withDocker ? Snippet("docker") : string.Empty;
        tokens["KitsJob"] = withKits ? Snippet("kits") : string.Empty;
        tokens["MainBranchSteps"] = family == GitHosts.Bitbucket && (withDocker || withKits)
            ? "  branches:\n    main:\n      - step: *build-test\n" + (withDocker ? Snippet("docker") : string.Empty) + (withKits ? Snippet("kits") : string.Empty)
            : string.Empty;

        var output = provider switch
        {
            GitHosts.GitHub => ".github/workflows/ci.yml",
            GitHosts.Gitea => ".gitea/workflows/ci.yml",
            GitHosts.GitLab => ".gitlab-ci.yml",
            GitHosts.Azure => "azure-pipelines.yml",
            GitHosts.Bitbucket => "bitbucket-pipelines.yml",
            _ => throw new ArgumentException($"Unknown CI provider '{provider}'.")
        };

        var writer = new FileWriter(config.SolutionPath);
        if (!writer.Write(output, TemplateRenderer.RenderTemplate($"V2/ops/ci/{family}.yml.tpl", tokens)))
        {
            ToolHost.Error($"{output} already exists; delete it first to regenerate.");
            return false;
        }

        config.CiProvider = provider;
        ConfigManager.Save(config.SolutionPath, config);
        ToolHost.Success($"CI pipeline added: {output}");
        return true;
    }

    // --- shared ------------------------------------------------------------------------------------------------------
    private static void RequireV2(SolutionConfig config)
    {
        if (!config.IsV2)
            throw new InvalidOperationException("This operation needs a layout v2 solution (dotnet-arch.yml: layout: v2).");
    }

    internal static Dictionary<string, string> Tokens(SolutionConfig config)
    {
        var app = config.SolutionName;
        var lower = Naming.ToKebabCase(app.Replace('.', '-')).Replace("--", "-");
        var imageBase = $"{lower}-api";
        var registry = (config.DockerRegistry ?? string.Empty).Trim().TrimEnd('/');
        var sourceName = string.IsNullOrWhiteSpace(config.NuGetSourceName) ? "private" : config.NuGetSourceName;

        return new Dictionary<string, string>
        {
            ["App"] = app,
            ["DotnetMajor"] = PackageVersionResolver.ResolveTargetMajor(config.TargetFramework).ToString(),
            ["Port"] = string.IsNullOrWhiteSpace(config.ApiPort) ? "8080" : config.ApiPort,
            ["ImageName"] = registry.Length == 0 ? imageBase : $"{registry}/{imageBase}",
            ["ContainerName"] = imageBase,
            ["DatabaseName"] = app.Replace('.', '_').ToLowerInvariant(),
            ["RegistryHost"] = RegistryHost(registry),
            ["NuGetSource"] = config.NuGetSource ?? string.Empty,
            ["NuGetSourceName"] = sourceName,
            ["NuGetSourceEnvName"] = Regex.Replace(sourceName, "[^A-Za-z0-9]", "_"),
            ["RestoreInstruction"] = string.Empty,
            ["NuGetConfigCopy"] = string.Empty,
            ["ComposeEnvLines"] = string.Empty,
            ["NuGetEnv"] = string.Empty,
            ["NuGetExport"] = string.Empty,
            ["DockerJob"] = string.Empty,
            ["KitsJob"] = string.Empty,
            ["MainBranchSteps"] = string.Empty,
        };
    }

    private static string RegistryHost(string? registry)
    {
        var value = (registry ?? string.Empty).Trim();
        var slash = value.IndexOf('/');
        return slash < 0 ? value : value[..slash];
    }

    private static string NuGetCredentialName(SolutionConfig config) =>
        "NuGetPackageSourceCredentials_" + Regex.Replace(string.IsNullOrWhiteSpace(config.NuGetSourceName) ? "private" : config.NuGetSourceName, "[^A-Za-z0-9]", "_");

    private static string NuGetEnv(SolutionConfig config, string family)
    {
        if (string.IsNullOrWhiteSpace(config.NuGetSource))
            return string.Empty;

        var name = NuGetCredentialName(config);
        return family switch
        {
            GitHosts.GitHub => $"  {name}: \"Username=${{{{ secrets.NUGET_USER }}}};Password=${{{{ secrets.NUGET_PASSWORD }}}}\"\n",
            GitHosts.Azure => $"  {name}: \"Username=$(NUGET_USER);Password=$(NUGET_PASSWORD)\"\n",
            _ => $"  {name}: \"Username=$NUGET_USER;Password=$NUGET_PASSWORD\"\n",
        };
    }

    private static string NuGetExport(SolutionConfig config) =>
        string.IsNullOrWhiteSpace(config.NuGetSource)
            ? string.Empty
            : $"          - export {NuGetCredentialName(config)}=\"Username=$NUGET_USER;Password=$NUGET_PASSWORD\"\n";
}
