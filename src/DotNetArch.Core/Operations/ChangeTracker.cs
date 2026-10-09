namespace DotNetArch.Core.Operations;

/// <summary>Detects which files an action created or modified by comparing two snapshots of a folder.</summary>
internal sealed class ChangeTracker
{
    private static readonly string[] Ignored = { ".git", "bin", "obj", "node_modules", "artifacts", ".vs", ".idea" };

    private readonly string _root;
    private readonly Dictionary<string, (long Length, long Ticks)> _before;

    private ChangeTracker(string root, Dictionary<string, (long, long)> before)
    {
        _root = root;
        _before = before;
    }

    public static ChangeTracker Begin(string root) => new(root, Snapshot(root));

    public (IReadOnlyList<string> Created, IReadOnlyList<string> Modified) End()
    {
        var after = Snapshot(_root);
        var created = after.Keys.Where(path => !_before.ContainsKey(path)).Order(StringComparer.Ordinal).ToList();
        var modified = after.Where(entry => _before.TryGetValue(entry.Key, out var old) && old != entry.Value).Select(entry => entry.Key).Order(StringComparer.Ordinal).ToList();
        return (created, modified);
    }

    /// <summary>Copies a folder without build output and VCS data (used to run a generator in plan mode).</summary>
    public static void Copy(string source, string target)
    {
        Directory.CreateDirectory(target);
        if (!Directory.Exists(source))
            return;
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            if (relative.Split(Path.DirectorySeparatorChar).Any(segment => Ignored.Contains(segment, StringComparer.OrdinalIgnoreCase)))
                continue;
            Directory.CreateDirectory(Path.Combine(target, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(segment => Ignored.Contains(segment, StringComparer.OrdinalIgnoreCase)))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, relative))!);
            File.Copy(file, Path.Combine(target, relative), overwrite: true);
        }
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
