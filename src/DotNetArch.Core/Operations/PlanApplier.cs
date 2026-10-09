using DotNetArch.Core.Hosting;

namespace DotNetArch.Core.Operations;

/// <summary>Writes a fix plan to disk: moves first, then file contents, then the follow-up commands (solution registration).</summary>
internal static class PlanApplier
{
    public static void Apply(string root, IReadOnlyList<FixAction> plan)
    {
        foreach (var move in plan.Where(a => a.MoveFrom != null))
        {
            var source = Path.Combine(root, move.MoveFrom!);
            var target = Path.Combine(root, move.Change.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(source))
            {
                File.Move(source, target);
                RemoveEmptyParents(root, Path.GetDirectoryName(source)!);
            }
            else
            {
                Directory.Move(source, target);
            }
        }

        foreach (var action in plan.Where(a => a.MoveFrom == null))
        {
            var path = Path.Combine(root, action.Change.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, action.Content);
        }

        foreach (var action in plan.Where(a => a.AfterWrite != null))
            action.AfterWrite!();
    }

    /// <summary>Folders left empty by a file move are removed (up to, never including, the repository root).</summary>
    private static void RemoveEmptyParents(string root, string directory)
    {
        var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        for (var current = Path.GetFullPath(directory); current.Length > rootPath.Length && Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any(); current = Path.GetDirectoryName(current)!)
            Directory.Delete(current);
    }

}
