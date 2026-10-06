using DotNetArch.Core.Hosting;
using Xunit;

namespace DotNetArch.Core.Tests;

public class CommandLineTests
{
    [Fact]
    public void Splits_arguments_and_keeps_quoted_values_together()
    {
        var spec = CommandLine.Parse("docker ps -a --filter name=api --format \"{{.Names}}\"", "/work");
        Assert.Equal("docker", spec.FileName);
        Assert.Equal(new[] { "ps", "-a", "--filter", "name=api", "--format", "{{.Names}}" }, spec.Arguments);
        Assert.Equal("/work", spec.WorkingDirectory);
        Assert.Null(spec.Environment);
    }

    [Fact]
    public void Leading_assignments_become_environment_variables()
    {
        var spec = CommandLine.Parse("ASPNETCORE_ENVIRONMENT=development docker compose build");
        Assert.Equal("docker", spec.FileName);
        Assert.Equal(new[] { "compose", "build" }, spec.Arguments);
        Assert.Equal("development", spec.Environment!["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public void Shell_operators_are_not_interpreted()
    {
        var spec = CommandLine.Parse("dotnet build && echo pwned");
        Assert.Equal(new[] { "build", "&&", "echo", "pwned" }, spec.Arguments);
    }

    [Fact]
    public void Empty_quoted_argument_is_preserved()
    {
        var spec = CommandLine.Parse("git commit -m \"\"");
        Assert.Equal(new[] { "commit", "-m", "" }, spec.Arguments);
    }

    [Fact]
    public void Unterminated_quote_is_rejected() =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse("git commit -m \"init"));

    [Fact]
    public void Blank_command_is_rejected() =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse("   "));
}
