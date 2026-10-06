using DotNetArch.Core.Hosting;

namespace DotNetArch.Core.Tests.Support;

/// <summary>Records every process request and answers with a configurable result; never starts a process.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Calls { get; } = new();

    public bool NextSuccess { get; set; } = true;

    public bool CancelRequested { get; private set; }

    public void Cancel() => CancelRequested = true;

    public void ResetCancel() => CancelRequested = false;

    public int RunInteractive(ProcessSpec spec)
    {
        Calls.Add(spec);
        return 0;
    }

    public ProcessResult Run(ProcessSpec spec, bool showProgress)
    {
        Calls.Add(spec);
        return new ProcessResult(NextSuccess, NextSuccess ? 0 : 1, NextSuccess ? "out" : "boom");
    }
}
