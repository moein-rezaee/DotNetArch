namespace DotNetArch.Mcp;

/// <summary>Detects which files a tool call created or modified by comparing two snapshots of a folder.</summary>
internal sealed class FileChanges
{
    private static readonly string[] Ignored = { ".git", "bin", "obj", "node_modules", "artifacts", ".vs", ".idea" };

    private readonly string _root;
    private readonly Dictionary<string, (long Length, long Ticks)> _before;

    private FileChanges(string root, Dictionary<string, (long, long)> before)
    {
        _root = root;
        _before = before;
    }

    public static FileChanges Begin(string root) => new(root, Snapshot(root));

    public (IReadOnlyList<string> Created, IReadOnlyList<string> Modified) End()
    {
        var after = Snapshot(_root);
        var created = after.Keys.Where(path => !_before.ContainsKey(path)).Order(StringComparer.Ordinal).ToList();
        var modified = after.Where(entry => _before.TryGetValue(entry.Key, out var old) && old != entry.Value)
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
        return (created, modified);
    }

    private static Dictionary<string, (long, long)> Snapshot(string root)
    {
        var files = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
        if (!Directory.Exists(root))
            return files;

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                if (!Ignored.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
                    pending.Push(sub);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var info = new FileInfo(file);
                files[Path.GetRelativePath(root, file).Replace('\\', '/')] = (info.Length, info.LastWriteTimeUtc.Ticks);
            }
        }

        return files;
    }
}
