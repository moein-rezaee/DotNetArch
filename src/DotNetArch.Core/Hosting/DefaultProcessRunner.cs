using System.Diagnostics;
using System.Text;

namespace DotNetArch.Core.Hosting;

/// <summary>Runs child processes directly (no shell), capturing combined output. No console interaction.</summary>
public class DefaultProcessRunner : IProcessRunner
{
    private readonly object _gate = new();
    private Process? _current;
    private volatile bool _cancelRequested;

    public bool CancelRequested => _cancelRequested;

    public void Cancel()
    {
        _cancelRequested = true;
        lock (_gate)
        {
            try { _current?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
    }

    public void ResetCancel() => _cancelRequested = false;

    public int RunInteractive(ProcessSpec spec)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            UseShellExecute = false,
            WorkingDirectory = spec.WorkingDirectory ?? Directory.GetCurrentDirectory()
        };
        foreach (var argument in spec.Arguments)
            startInfo.ArgumentList.Add(argument);
        ApplyEnvironment(startInfo, spec);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Cannot start '{spec.FileName}'.");
        lock (_gate) _current = process;
        process.WaitForExit(int.MaxValue);
        lock (_gate) _current = null;
        return process.ExitCode;
    }

    public ProcessResult Run(ProcessSpec spec, bool showProgress)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = spec.WorkingDirectory ?? Directory.GetCurrentDirectory()
        };
        foreach (var argument in spec.Arguments)
            startInfo.ArgumentList.Add(argument);
        ApplyEnvironment(startInfo, spec);

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Collect(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            lock (output) output.AppendLine(e.Data);
            lines.Enqueue(e.Data);
        }
        process.OutputDataReceived += Collect;
        process.ErrorDataReceived += Collect;

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProcessResult(false, -1, $"Cannot start '{spec.FileName}': {ex.Message}");
        }

        lock (_gate) _current = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var progress = showProgress ? Task.Run(() => ShowProgress(process, lines)) : null;
        WaitForExitAndOutput(process);
        progress?.Wait();
        lock (_gate) _current = null;

        string text;
        lock (output) text = output.ToString();
        return new ProcessResult(process.ExitCode == 0 && !_cancelRequested, process.ExitCode, text);
    }

    private static void ApplyEnvironment(ProcessStartInfo startInfo, ProcessSpec spec)
    {
        // Long-lived MSBuild nodes started by `dotnet` would inherit our pipes and linger for minutes after the command finished.
        if (spec.FileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        }

        if (spec.Environment is not null)
            foreach (var (key, value) in spec.Environment)
                startInfo.Environment[key] = value;
    }

    /// <summary>
    /// Waits for the process to exit, then gives the redirected streams a short grace period to flush. Plain
    /// <c>WaitForExit()</c> would also wait for EOF, which never comes while an orphaned grandchild keeps the pipe open.
    /// </summary>
    private static void WaitForExitAndOutput(Process process)
    {
        process.WaitForExit(int.MaxValue);
        Task.Run(() => process.WaitForExit()).Wait(TimeSpan.FromSeconds(3));
    }

    /// <summary>Hook for interactive runners; the default does nothing but drain.</summary>
    protected virtual void ShowProgress(Process process, System.Collections.Concurrent.ConcurrentQueue<string> lines)
    {
        while (!process.HasExited || !lines.IsEmpty)
        {
            while (lines.TryDequeue(out _)) { }
            Thread.Sleep(50);
        }
    }
}
