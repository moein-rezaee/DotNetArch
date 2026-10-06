using DotNetArch.Core.Hosting;
using Xunit;

namespace DotNetArch.Core.Tests;

public class HostTests
{
    private sealed class FakeRunner : IProcessRunner
    {
        public List<ProcessSpec> Calls { get; } = new();
        public bool NextSuccess { get; set; } = true;
        public bool CancelRequested { get; private set; }
        public void Cancel() => CancelRequested = true;
        public void ResetCancel() => CancelRequested = false;
        public int RunInteractive(ProcessSpec spec) { Calls.Add(spec); return 0; }
        public ProcessResult Run(ProcessSpec spec, bool showProgress)
        {
            Calls.Add(spec);
            return new ProcessResult(NextSuccess, NextSuccess ? 0 : 1, NextSuccess ? "out" : "boom");
        }
    }

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
        var runner = new FakeRunner();
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
        var custom = new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), new FakeRunner());
        using (ToolHost.Use(custom))
            Assert.Same(custom, ToolHost.Current);
        Assert.Same(before, ToolHost.Current);
    }
}
