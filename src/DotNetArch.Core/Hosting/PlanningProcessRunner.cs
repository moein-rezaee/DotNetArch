namespace DotNetArch.Core.Hosting;

/// <summary>
/// Runs a generator in plan mode: commands that only create or register project files (<c>dotnet new</c>, <c>dotnet sln</c>, <c>git init</c>, queries) really run in the
/// throw-away copy, while the expensive or outward ones (builds, restores, EF migrations and database updates, tool installs, Docker, remotes) are answered
/// with success without running. A plan therefore lists the files the real run writes without building anything or touching a database.
/// </summary>
public sealed class PlanningProcessRunner(IProcessRunner inner) : IProcessRunner
{
    private static readonly string[] SkippedDotnet = { "build", "restore", "ef", "tool", "publish", "test", "run", "format", "pack", "nuget" };

    public bool CancelRequested => inner.CancelRequested;

    public void Cancel() => inner.Cancel();

    public void ResetCancel() => inner.ResetCancel();

    public int RunInteractive(ProcessSpec spec) => 0;

    public ProcessResult Run(ProcessSpec spec, bool showProgress)
    {
        var name = Path.GetFileNameWithoutExtension(spec.FileName).ToLowerInvariant();
        var first = spec.Arguments.FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        var skip = name switch
        {
            "dotnet" => SkippedDotnet.Contains(first) || (first == "add" && spec.Arguments.Any(a => a.Equals("package", StringComparison.OrdinalIgnoreCase))),
            "git" => first is "remote" or "push" or "pull" or "fetch" or "clone",
            _ => true,
        };
        return skip ? new ProcessResult(true, 0, string.Empty) : inner.Run(spec, showProgress: false);
    }
}
