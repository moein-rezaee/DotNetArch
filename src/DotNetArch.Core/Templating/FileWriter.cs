namespace DotNetArch.Core.Templating;

/// <summary>Writes generated files with LF line endings and reports what changed. Existing files are never overwritten unless asked.</summary>
public sealed class FileWriter(string root)
{
    private readonly List<string> _created = new();
    private readonly List<string> _skipped = new();

    public string Root { get; } = root;

    public IReadOnlyList<string> Created => _created;

    public IReadOnlyList<string> Skipped => _skipped;

    /// <summary>Writes <paramref name="relativePath"/> under the root. Returns false when the file already exists and <paramref name="overwrite"/> is false.</summary>
    public bool Write(string relativePath, string content, bool overwrite = false)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        var rootFull = Path.GetFullPath(Root);
        if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal) && full != rootFull)
            throw new InvalidOperationException($"Refusing to write outside the solution root: {relativePath}");

        if (File.Exists(full) && !overwrite)
        {
            _skipped.Add(relativePath);
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content.Replace("\r\n", "\n"), new System.Text.UTF8Encoding(false));
        _created.Add(relativePath);
        return true;
    }

    public bool Exists(string relativePath) => File.Exists(Path.Combine(Root, relativePath));
}
