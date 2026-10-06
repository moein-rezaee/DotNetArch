using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

public sealed class OpsGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-ops-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public OpsGenerationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SolutionConfig Generate(string provider, OpsOptions ops)
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        SolutionGenerator.Generate(new SolutionRequest("Shop", _root, "Shop.Api", "controller", provider, Ops: ops));
        return ConfigManager.Load(Path.Combine(_root, "Shop"))!;
    }

    private string Read(string relative) => File.ReadAllText(Path.Combine(_root, "Shop", relative));

    private bool Exists(string relative) => File.Exists(Path.Combine(_root, "Shop", relative));

    [Theory]
    [InlineData("https://github.com/acme/shop.git", "github")]
    [InlineData("git@github.com:acme/shop.git", "github")]
    [InlineData("https://gitlab.com/acme/shop.git", "gitlab")]
    [InlineData("git@gitlab.mycorp.io:team/shop.git", "gitlab")]
    [InlineData("https://dev.azure.com/org/proj/_git/shop", "azure")]
    [InlineData("git@ssh.dev.azure.com:v3/org/proj/shop", "azure")]
    [InlineData("https://bitbucket.org/acme/shop.git", "bitbucket")]
    [InlineData("https://gitea.example.com/acme/shop.git", "gitea")]
    [InlineData("https://git.example.com/acme/shop.git", null)]
    [InlineData("", null)]
    public void Detect_maps_remotes_to_providers(string remote, string? expected) =>
        Assert.Equal(expected, GitHosts.Detect(remote));

    [Theory]
    [InlineData("github", ".github/workflows/ci.yml")]
    [InlineData("gitlab", ".gitlab-ci.yml")]
    [InlineData("azure", "azure-pipelines.yml")]
    [InlineData("bitbucket", "bitbucket-pipelines.yml")]
    [InlineData("gitea", ".gitea/workflows/ci.yml")]
    public void Each_ci_provider_gets_its_own_pipeline_file(string provider, string file)
    {
        var config = Generate("SQLite", new OpsOptions(Ci: provider, NoGit: true));

        Assert.Equal(provider, config.CiProvider);
        Assert.True(Exists(file), $"missing {file}");
        Assert.DoesNotContain("{{", Read(file));
        Assert.Contains("validate-examples.sh", Read(file));
        Assert.DoesNotContain("docker login", Read(file)); // no registry configured: no push job
    }

    [Fact]
    public void Ci_is_detected_from_the_git_remote_and_none_skips_it()
    {
        var detected = Generate("SQLite", new OpsOptions(GitRemote: "https://gitlab.com/acme/shop.git"));
        Assert.Equal("gitlab", detected.CiProvider);
        Assert.True(Exists(".gitlab-ci.yml"));
    }

    [Fact]
    public void Ci_none_generates_no_pipeline()
    {
        var config = Generate("SQLite", new OpsOptions(Ci: "none", NoGit: true));
        Assert.Equal(string.Empty, config.CiProvider);
        Assert.False(Exists(".github/workflows/ci.yml") || Exists(".gitlab-ci.yml"));
    }

    [Fact]
    public void Unknown_ci_provider_is_rejected()
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        Assert.Throws<ArgumentException>(() =>
            SolutionGenerator.Generate(new SolutionRequest("Shop", _root, "Shop.Api", "controller", "SQLite", Ops: new OpsOptions(Ci: "jenkins", NoGit: true))));
    }

    [Fact]
    public void Personal_git_host_and_registries_flow_into_config_ci_compose_and_nuget()
    {
        var config = Generate("Postgres", new OpsOptions(
            Ci: "github",
            GitRemote: "git@git.mycorp.io:team/shop.git",
            GitProvider: "gitlab",
            DockerRegistry: "registry.mycorp.io/team",
            NuGetSource: "https://nuget.mycorp.io/v3/index.json",
            NuGetSourceName: "corp-feed"));

        Assert.Equal("git.mycorp.io", config.GitHost);
        Assert.Equal("gitlab", config.GitProvider);
        Assert.Equal("registry.mycorp.io/team", config.DockerRegistry);

        var ci = Read(".github/workflows/ci.yml");
        Assert.Contains("registry: registry.mycorp.io", ci);
        Assert.Contains("registry.mycorp.io/team/shop-api", ci);
        Assert.Contains("NuGetPackageSourceCredentials_corp_feed", ci);
        Assert.Contains("secrets.REGISTRY_PASSWORD", ci);

        Assert.Contains("image: registry.mycorp.io/team/shop-api:", Read("docker-compose.yml"));
        Assert.Contains("https://nuget.mycorp.io/v3/index.json", Read("NuGet.config"));
        Assert.Contains("dotnet restore", Read("src/Shop.Api/Dockerfile"));
        Assert.Contains("type=secret,id=nuget_config", Read("src/Shop.Api/Dockerfile"));

        // credentials are never written to any generated file
        var all = string.Join("\n", Directory.EnumerateFiles(Path.Combine(_root, "Shop"), "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
        Assert.DoesNotContain("Password=hunter2", all);
    }

    [Fact]
    public void Nuget_source_with_embedded_credentials_is_rejected()
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        Assert.Throws<ArgumentException>(() => SolutionGenerator.Generate(new SolutionRequest(
            "Shop", _root, "Shop.Api", "controller", "SQLite",
            Ops: new OpsOptions(Ci: "none", NoGit: true, NuGetSource: "https://user:pw@nuget.mycorp.io/v3/index.json"))));
    }

    [Theory]
    [InlineData("SQLite", "Data Source=/data/app.db", "volumes:")]
    [InlineData("Postgres", "POSTGRES_PASSWORD", "image: postgres:16")]
    [InlineData("SqlServer", "SQLSERVER_PASSWORD", "mssql/server:2022-latest")]
    public void Compose_matches_the_database_provider(string provider, string expected, string alsoExpected)
    {
        Generate(provider, new OpsOptions(Ci: "none", NoGit: true));

        var compose = Read("docker-compose.yml");
        Assert.Contains(expected, compose);
        Assert.Contains(alsoExpected, compose);
        Assert.DoesNotContain("{{", compose);
        Assert.True(Exists("src/Shop.Api/Dockerfile"));
        Assert.True(Exists(".dockerignore"));
        Assert.Contains("USER $APP_UID", Read("src/Shop.Api/Dockerfile"));
    }

    [Fact]
    public void No_docker_flag_skips_container_files()
    {
        Generate("SQLite", new OpsOptions(Ci: "none", NoGit: true, NoDocker: true));
        Assert.False(Exists("docker-compose.yml"));
        Assert.False(Exists("src/Shop.Api/Dockerfile"));
    }

    [Fact]
    public void Shell_scripts_are_executable_on_unix()
    {
        Generate("SQLite", new OpsOptions(Ci: "none", NoGit: true, NoDocker: true));
        if (OperatingSystem.IsWindows())
            return;

        var mode = File.GetUnixFileMode(Path.Combine(_root, "Shop", "scripts", "validate-examples.sh"));
        Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
    }
}
