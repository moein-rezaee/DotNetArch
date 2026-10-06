using DotNetArch.Core.Config;
using Xunit;

namespace DotNetArch.Core.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotnet-arch-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigManagerTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Save_then_load_round_trips_all_fields()
    {
        var config = new SolutionConfig
        {
            SolutionName = "Acme",
            SolutionPath = _dir,
            StartupProject = "Acme.API",
            DatabaseProvider = "Postgres",
            ApiStyle = "fast",
            ApiPort = "5123",
            TargetFramework = "net9.0",
            DockerImage = "acme.api",
            DockerContainer = "acme-api",
        };
        config.Entities["Product"] = new EntityStatus { HasCrud = true, HasAction = true };
        config.Entities["Order"] = new EntityStatus { HasCrud = true };

        ConfigManager.Save(_dir, config);
        var loaded = ConfigManager.Load(_dir)!;

        Assert.Equal("Acme", loaded.SolutionName);
        Assert.Equal("Postgres", loaded.DatabaseProvider);
        Assert.Equal("fast", loaded.ApiStyle);
        Assert.Equal("5123", loaded.ApiPort);
        Assert.Equal("net9.0", loaded.TargetFramework);
        Assert.Equal("acme.api", loaded.DockerImage);
        Assert.True(loaded.Entities["Product"].HasCrud && loaded.Entities["Product"].HasAction);
        Assert.True(loaded.Entities["Order"].HasCrud && !loaded.Entities["Order"].HasAction);
    }

    [Fact]
    public void Load_returns_null_without_config_file() => Assert.Null(ConfigManager.Load(_dir));

    [Fact]
    public void Load_defaults_startup_project_and_path()
    {
        File.WriteAllText(Path.Combine(_dir, "dotnet-arch.yml"), "solution: Acme\n");
        var loaded = ConfigManager.Load(_dir)!;
        Assert.Equal("Acme.API", loaded.StartupProject);
        Assert.Equal(_dir, loaded.SolutionPath);
    }
}
