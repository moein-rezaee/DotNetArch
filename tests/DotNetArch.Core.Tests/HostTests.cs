using DotNetArch.Core.Hosting;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

public class HostTests
{
    [Fact]
    public void NonInteractivePrompter_uses_defaults_and_fails_for_free_text()
    {
        var prompter = new NonInteractivePrompter();
        Assert.True(prompter.AskYesNo("x?", true));
        Assert.Equal("fallback", prompter.Ask("name", "fallback"));
        Assert.Throws<MissingInputException>(() => prompter.Ask("name"));
        Assert.Equal("b", prompter.AskOption("pick", new[] { "a", "b", "c" }, 0, new[] { 0 }));
    }

    [Fact]
    public void RunCommand_reports_through_output_and_never_uses_a_shell()
    {
        var runner = new FakeProcessRunner();
        var output = new BufferedToolOutput();
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), output, runner));

        Assert.True(ToolHost.RunCommand("dotnet build", "/work"));
        var call = Assert.Single(runner.Calls);
        Assert.Equal("dotnet", call.FileName);
        Assert.Equal(new[] { "build" }, call.Arguments);
        Assert.Contains("dotnet build", output.ToString());

        runner.NextSuccess = false;
        Assert.False(ToolHost.RunCommand("dotnet build", "/work"));
        Assert.Contains("boom", output.ToString());
    }

    [Fact]
    public void Use_restores_previous_host_on_dispose()
    {
        var before = ToolHost.Current;
        var custom = new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeProcessRunner());
        using (ToolHost.Use(custom))
            Assert.Same(custom, ToolHost.Current);
        Assert.Same(before, ToolHost.Current);
    }
}
