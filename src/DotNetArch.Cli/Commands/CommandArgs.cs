namespace DotNetArch.Cli.Commands;

/// <summary>Parsed command-line tail: <c>--key=value</c> options, bare <c>--flag</c>s and positional values.</summary>
internal sealed class CommandArgs
{
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Positionals { get; } = new();

    public static CommandArgs Parse(string[] args, int startIndex)
    {
        var parsed = new CommandArgs();
        for (var i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                parsed.Positionals.Add(arg);
                continue;
            }

            var eq = arg.IndexOf('=');
            if (eq > 0)
                parsed._options[arg[..eq]] = arg[(eq + 1)..];
            else
                parsed._flags.Add(arg);
        }
        return parsed;
    }

    /// <summary>Value of <c>--name=value</c> (name given without dashes) or null.</summary>
    public string? Get(string name) => _options.TryGetValue("--" + name, out var value) ? value : null;

    public bool Has(string flag) => _flags.Contains("--" + flag);
}
