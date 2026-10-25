using {{App}}.Cli.Tests.Support;

namespace {{App}}.Cli.Tests;

public class CliBasicsTests
{
    [Fact]
    public async Task Help_lists_every_command_and_exits_with_zero()
    {
        var harness = new CliHarness();

        var (exitCode, output, _) = await harness.RunAsync("help");

        Assert.Equal(0, exitCode);
        foreach (var command in harness.Commands)
            Assert.Contains(command.Name, output);
    }

    [Fact]
    public async Task No_arguments_and_unknown_commands_are_usage_errors()
    {
        var harness = new CliHarness();

        Assert.Equal(2, (await harness.RunAsync()).ExitCode);
        var (exitCode, _, error) = await harness.RunAsync("nothing", "here");
        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown command", error);
    }

    [Fact]
    public void Every_command_has_a_unique_tool_name()
    {
        var names = new CliHarness().Commands.Select(c => c.Tool).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
