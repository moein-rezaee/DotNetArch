using System.Collections.Concurrent;
using System.Diagnostics;
using DotNetArch.Core.Hosting;

namespace DotNetArch.Cli.Console;

/// <summary>Process runner that animates a spinner with the last output line when attached to a real terminal.</summary>
public sealed class ConsoleProcessRunner : DefaultProcessRunner
{
    private static readonly char[] Frames = { '⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷' };

    protected override void ShowProgress(Process process, ConcurrentQueue<string> lines)
    {
        // Without an interactive terminal (CI, pipes) there is nothing to animate: just drain the output.
        if (System.Console.IsOutputRedirected || SafeWindowWidth() <= 4)
        {
            base.ShowProgress(process, lines);
            return;
        }

        var idx = 0;
        var last = string.Empty;
        var color = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        while (!process.HasExited || !lines.IsEmpty)
        {
            while (lines.TryDequeue(out var line))
                last = line;
            var max = Math.Max(0, SafeWindowWidth() - 2);
            if (last.Length > max)
                last = last[..max];
            System.Console.Write($"\r{Frames[idx++ % Frames.Length]} {last}");
            Thread.Sleep(120);
        }
        System.Console.ForegroundColor = color;
        System.Console.Write("\r" + new string(' ', SafeWindowWidth()) + "\r");
    }

    private static int SafeWindowWidth()
    {
        try { return System.Console.WindowWidth; }
        catch (IOException) { return 0; }
    }
}
