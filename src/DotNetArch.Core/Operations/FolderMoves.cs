using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Profile-driven folder moves (DA-A12): a profile lists root folders that belong elsewhere in the standard layout (<c>folder_moves: [{from, to}]</c>).
/// The folder moves as it is (no file is edited inside it) and the paths that point at it are rewritten in project files, Dockerfiles, Compose, CI and
/// documentation (history documents and code are left alone). The tool names no folder itself; the profile does.
/// </summary>
internal static class FolderMoves
{
    public static IReadOnlyList<NetArch.FolderMove> Pending(RepoContext ctx) =>
        (ctx.Profile?.FolderMoves ?? new List<NetArch.FolderMove>())
        .Where(m => m.From.Length > 0 && m.To.Length > 0 && !m.From.Contains('/') && ctx.DirectoryExists(m.From) && !ctx.DirectoryExists(m.To))
        .ToList();

    public static IReadOnlyList<FixAction> Plan(RepoContext ctx)
    {
        var moves = Pending(ctx);
        var actions = new List<FixAction>();
        foreach (var move in moves)
            actions.Add(new FixAction(new PlannedChange(move.To, "move", $"moved from {move.From}", "DA-A12"), string.Empty, MoveFrom: move.From));

        foreach (var file in ctx.Files)
        {
            var isProject = file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);
            if (moves.Any(m => file.StartsWith(m.From + "/", StringComparison.OrdinalIgnoreCase)) || !(isProject || StructuralFixers.IsTextRewriteTarget(file)))
                continue;
            var text = ctx.Read(file);
            if (text.Length == 0)
                continue;
            var rewritten = text;
            foreach (var move in moves)
                rewritten = Rewrite(rewritten, move.From, move.To);
            if (rewritten != text)
                actions.Add(new FixAction(new PlannedChange(file, "modify", "paths follow the moved folder", "DA-A12"), rewritten));
        }

        return actions;
    }

    /// <summary>
    /// A path segment <paramref name="from"/> at the start of a repository-relative path (or right under <c>/src/&lt;repo&gt;/</c> in a Dockerfile) becomes <paramref name="to"/>.
    /// Not touched: the same word inside another path (<c>/app/ocelot</c>, <c>docs/ocelot/</c>), a key path without a file or glob after it, and MSBuild <c>Link</c> metadata (an output location, not a source).
    /// </summary>
    internal static string Rewrite(string text, string from, string to)
    {
        var name = Regex.Escape(from);
        var tail = @"(?=[/\\](?:\*|[\w.-]+\.\w+|(?![\w-])))";
        var start = @"(?:(?<![\w./\\-])|(?<=/src/[\w.-]+/))";
        text = Regex.Replace(text, $@"(?<!Link="")" + start + $@"(?<dot>(?:\.\.?[/\\])*){name}(?=/(?:\*|[\w.-]+\.\w+|(?![\w-])))", m => m.Groups["dot"].Value + to, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        return Regex.Replace(text, $@"(?<!Link="")" + start + $@"(?<dot>(?:\.\.?[/\\])*){name}(?=\\(?:\*|[\w.-]+\.\w+|(?![\w-])))", m => m.Groups["dot"].Value + to.Replace('/', '\\'), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }
}
