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
        if (spec.Environment is not null)
            foreach (var (key, value) in spec.Environment)
                startInfo.Environment[key] = value;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Cannot start '{spec.FileName}'.");
        lock (_gate) _current = process;
        process.WaitForExit();
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
        if (spec.Environment is not null)
            foreach (var (key, value) in spec.Environment)
                startInfo.Environment[key] = value;

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
        process.WaitForExit();
        progress?.Wait();
        lock (_gate) _current = null;

        string text;
        lock (output) text = output.ToString();
        return new ProcessResult(process.ExitCode == 0 && !_cancelRequested, process.ExitCode, text);
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
