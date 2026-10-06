namespace {{App}}.Infrastructure.Configuration;

/// <summary>
/// Minimal <c>.env</c> reader: <c>KEY=value</c> lines, <c>#</c> comments, optional quotes. <c>__</c> in a key means a
/// configuration section separator (same convention as environment variables).
/// </summary>
public static class EnvFile
{
    public static IReadOnlyDictionary<string, string?> Read(string path)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return values;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            values[key.Replace("__", ":", StringComparison.Ordinal)] = value;
        }

        return values;
    }
}
