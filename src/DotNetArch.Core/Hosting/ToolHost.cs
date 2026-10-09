namespace DotNetArch.Core.Hosting;

/// <summary>The services Core needs from whoever hosts it (console, MCP server, tests).</summary>
public sealed record HostContext(IPrompter Prompter, IToolOutput Output, IProcessRunner Runner, bool SkipMigrations = false, bool Planning = false);

/// <summary>
/// Ambient access to the host (D-18). <see cref="Configure"/> sets the process default; <see cref="Use"/> overrides it
/// for the current async flow (one MCP request, one test) and restores the previous value on dispose.
/// </summary>
public static class ToolHost
{
    private static readonly AsyncLocal<HostContext?> Override = new();
    private static HostContext _default = new(new NonInteractivePrompter(), new NullToolOutput(), new DefaultProcessRunner());

    public static HostContext Current => Override.Value ?? _default;

    public static IPrompter Prompter => Current.Prompter;

    public static IToolOutput Output => Current.Output;

    public static IProcessRunner Runner => Current.Runner;

    /// <summary>True when the host asked generators not to create EF migrations (they can be added later).</summary>
    public static bool SkipMigrations => Current.SkipMigrations;

    /// <summary>True while a generator runs in plan mode (a throw-away copy): nothing outside that copy may be written.</summary>
    public static bool Planning => Current.Planning;

    public static void Configure(HostContext context) => _default = context ?? throw new ArgumentNullException(nameof(context));

    public static IDisposable Use(HostContext context)
    {
        var previous = Override.Value;
        Override.Value = context ?? throw new ArgumentNullException(nameof(context));
        return new Restore(previous);
    }

    private sealed class Restore(HostContext? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }

    // --- prompts -----------------------------------------------------------------------------------------------
    public static string Ask(string message, string? defaultValue = null) => Prompter.Ask(message, defaultValue);

    public static bool AskYesNo(string message, bool defaultYes) => Prompter.AskYesNo(message, defaultYes);

    public static string AskOption(string message, string[] options, int defaultIndex = 0, int[]? disabledIndices = null) =>
        Prompter.AskOption(message, options, defaultIndex, disabledIndices);

    // --- output ------------------------------------------------------------------------------------------------
    public static void Info(string title, string? description = null) => Output.Section("ℹ️", title, description);

    public static void Success(string title, string? description = null) => Output.Section("✅", title, description);

    public static void Error(string title, string? description = null) => Output.Section("❌", title, description);

    public static void Step(string title, string? description = null)
    {
        Output.Blank();
        Output.Section("🔹", title, description);
    }

    public static void SubStep(bool success, string message) => Output.SubStep(success, message);

    public static void Blank() => Output.Blank();

    // --- processes ---------------------------------------------------------------------------------------------
    /// <summary>Runs a command line (parsed without a shell) and prints success/failure unless <paramref name="print"/> is false.</summary>
    public static bool RunCommand(string command, string? workingDir = null, bool print = true)
    {
        var result = Runner.Run(CommandLine.Parse(command, workingDir), print);
        if (Runner.CancelRequested)
            return false;
        if (print)
        {
            if (!result.Success)
                Error(string.IsNullOrWhiteSpace(result.Output) ? "Command failed" : result.Output.Trim());
            else
                Success(command);
        }
        return result.Success;
    }

    public static (bool Success, string Output) RunCommandCapture(string command, string? workingDir = null)
    {
        var result = Runner.Run(CommandLine.Parse(command, workingDir), showProgress: false);
        return (result.Success && !Runner.CancelRequested, result.Output);
    }
}
