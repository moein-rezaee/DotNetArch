namespace DotNetArch.Cli.Commands;

internal interface ICommand
{
    bool Matches(string[] args);

    /// <summary>Runs the command and returns the process exit code (0 ok, 1 usage/validation).</summary>
    int Run(string[] args);
}

internal static class CommandMatch
{
    public static bool Is(string[] args, string first, string? second = null) =>
        args.Length >= (second is null ? 1 : 2)
        && args[0].Equals(first, StringComparison.OrdinalIgnoreCase)
        && (second is null || args[1].Equals(second, StringComparison.OrdinalIgnoreCase));
}
