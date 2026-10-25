using System.Text.Json;
using {{App}}.Cli.Tests.Support;

namespace {{App}}.Cli.Tests.Commands;

/// <summary>The commands run the same use cases as the API and the MCP tools: these tests drive them through the real MediatR pipeline.</summary>
public class {{Plural}}CommandsTests
{
    private static Guid IdOf(string json) => JsonDocument.Parse(json).RootElement.GetProperty("id").GetGuid();

    [Fact]
    public async Task Create_get_list_update_and_delete_follow_the_same_rules_as_the_api()
    {
        var harness = new CliHarness();

        var created = await harness.RunAsync("{{CliGroup}}", "create", "--name=Widget");
        Assert.Equal(0, created.ExitCode);
        var id = IdOf(created.Output);

        Assert.Contains("Widget", (await harness.RunAsync("{{CliGroup}}", "get", $"--id={id}")).Output);
        Assert.Contains("Widget", (await harness.RunAsync("{{CliGroup}}", "list")).Output);
        Assert.Contains("Gadget", (await harness.RunAsync("{{CliGroup}}", "update", $"--id={id}", "--name=Gadget")).Output);
        Assert.Equal("Deleted.", (await harness.RunAsync("{{CliGroup}}", "delete", $"--id={id}")).Output.Trim());
        Assert.Equal(1, (await harness.RunAsync("{{CliGroup}}", "get", $"--id={id}")).ExitCode);
    }

    [Fact]
    public async Task Validation_failures_exit_with_one_and_missing_options_with_two()
    {
        var harness = new CliHarness();

        var invalid = await harness.RunAsync("{{CliGroup}}", "create", "--name=");
        Assert.Equal(1, invalid.ExitCode);
        Assert.Contains("Validation failed", invalid.Error);

        var missing = await harness.RunAsync("{{CliGroup}}", "get");
        Assert.Equal(2, missing.ExitCode);
        Assert.Contains("--id", missing.Error);
    }
}
