namespace DotNetArch.Core.Hosting;

/// <summary>A child process to run. Arguments are passed as a list; no shell is involved.</summary>
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

public sealed record ProcessResult(bool Success, int ExitCode, string Output);

public interface IProcessRunner
{
    /// <summary>Runs the process to completion. <paramref name="showProgress"/> lets interactive runners animate.</summary>
    ProcessResult Run(ProcessSpec spec, bool showProgress);

    /// <summary>Runs the process attached to the current terminal (stdin/stdout/stderr inherited) and returns its exit code.</summary>
    int RunInteractive(ProcessSpec spec);

    /// <summary>True once <see cref="Cancel"/> was called and until <see cref="ResetCancel"/>.</summary>
    bool CancelRequested { get; }

    /// <summary>Kills the running process tree (if any) and marks cancellation.</summary>
    void Cancel();

    void ResetCancel();
}
