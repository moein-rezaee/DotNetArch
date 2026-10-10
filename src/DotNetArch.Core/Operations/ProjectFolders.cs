using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// DA-A13: in the layouts v2 and v3 every project folder sits directly under <c>src/</c> (or the tests root) and carries the exact name of its project
/// (<c>src/Shop.Api</c>, not <c>src/api/Shop.Api</c> or <c>src/Extensions/Shop/Shop.Core</c>). The fixer moves the folders and rewrites every path that points
/// at them: project references (recomputed), the solution, Dockerfiles, Compose, CI and documentation. Code is not edited.
/// </summary>
internal static class ProjectFolders
{
    /// <summary>The folder a project should have, or null when it is right (or the layout has no src/ yet).</summary>
    public static IReadOnlyDictionary<string, string> Misplaced(RepoContext ctx)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ctx.Layout is not ("v2" or "v3"))
            return result;
        var testsRoot = ctx.Layout == "v3" ? "test" : "tests";
        foreach (var project in ctx.Projects.Where(p => p.Dir.Length > 0))
        {
            var expected = (project.IsTest ? testsRoot : "src") + "/" + project.Name;
            if (!string.Equals(project.Dir, expected, StringComparison.Ordinal) && (project.Dir.StartsWith("src/", StringComparison.Ordinal) || project.Dir.StartsWith(testsRoot + "/", StringComparison.Ordinal)))
                result[project.Dir] = expected;
        }

        return result;
    }

    public static IReadOnlyList<FixAction> Plan(RepoContext ctx)
    {
        var moved = Misplaced(ctx);
        if (moved.Count == 0)
            return Array.Empty<FixAction>();
        if (moved.Values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) || moved.Values.Any(v => ctx.DirectoryExists(v)))
            return Array.Empty<FixAction>();

        var actions = new List<FixAction>();
        foreach (var (oldDir, newDir) in moved.OrderBy(m => m.Key, StringComparer.Ordinal))
            actions.Add(new FixAction(new PlannedChange(newDir, "move", $"moved from {oldDir}", "DA-A13"), string.Empty, MoveFrom: oldDir));

        string NewDirOf(string oldDir) => moved.TryGetValue(oldDir, out var n) ? n : oldDir;
        var byFile = ctx.Projects.ToDictionary(p => p.File, p => p, StringComparer.OrdinalIgnoreCase);
        string NewFileOf(ProjectInfo p) => NewDirOf(p.Dir) + "/" + System.IO.Path.GetFileName(p.File);
        string NewPathOfFile(string file)
        {
            var owner = moved.Keys.Where(d => file.StartsWith(d + "/", StringComparison.Ordinal)).OrderByDescending(d => d.Length).FirstOrDefault();
            return owner == null ? file : moved[owner] + file[owner.Length..];
        }

        foreach (var file in ctx.Files)
        {
            var isProject = file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
            var isSolution = file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);
            var newPath = NewPathOfFile(file);
            if (!isProject && !isSolution && !StructuralFixers.IsTextRewriteTarget(file))
                continue;
            var text = ctx.Read(file);
            if (text.Length == 0)
                continue;
            string rewritten;
            if (isProject)
                rewritten = RewriteProject(text, file, newPath, byFile, NewFileOf);
            else if (isSolution)
                rewritten = RewriteSolution(text, moved);
            else
                rewritten = RewriteText(text, moved);
            if (rewritten != text || newPath != file)
            {
                if (rewritten != text)
                    actions.Add(new FixAction(new PlannedChange(newPath, "modify", "paths follow the moved projects", "DA-A13"), rewritten));
            }
        }

        return actions;
    }

    private static string RewriteProject(string text, string oldFile, string newFile, Dictionary<string, ProjectInfo> byFile, Func<ProjectInfo, string> newFileOf)
    {
        var oldDir = System.IO.Path.GetDirectoryName(oldFile)?.Replace('\\', '/') ?? string.Empty;
        var newDir = System.IO.Path.GetDirectoryName(newFile)?.Replace('\\', '/') ?? string.Empty;
        return Regex.Replace(text, @"(?<head><ProjectReference\s+Include="")(?<path>[^""]+)(?<tail>"")", m =>
        {
            var original = m.Groups["path"].Value;
            var backslash = original.Contains('\\', StringComparison.Ordinal);
            var absolute = Normalize(oldDir + "/" + original.Replace('\\', '/'));
            if (!byFile.TryGetValue(absolute, out var target))
                return m.Value;
            var relative = System.IO.Path.GetRelativePath(newDir.Length == 0 ? "." : newDir, newFileOf(target)).Replace('\\', '/');
            return m.Groups["head"].Value + (backslash ? relative.Replace('/', '\\') : relative) + m.Groups["tail"].Value;
        }, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private static string RewriteSolution(string text, IReadOnlyDictionary<string, string> moved)
    {
        foreach (var (oldDir, newDir) in moved.OrderByDescending(m => m.Key.Length))
            text = text.Replace(oldDir.Replace('/', '\\') + "\\", newDir.Replace('/', '\\') + "\\", StringComparison.Ordinal);
        return text;
    }

    private static string RewriteText(string text, IReadOnlyDictionary<string, string> moved)
    {
        foreach (var (oldDir, newDir) in moved.OrderByDescending(m => m.Key.Length))
        {
            text = Regex.Replace(text, $@"(?<![\w./\\-])(?<dot>(?:\.\.?/)*){Regex.Escape(oldDir)}(?=[/\\""'\s]|$)", $"${{dot}}{newDir}", RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromSeconds(2));
            // a Dockerfile copies from the build stage's working directory: COPY --from=build /src/src/api/App/x /app/x
            text = Regex.Replace(text, $@"(?<=/src/){Regex.Escape(oldDir)}(?=[/\\""'\s]|$)", newDir, RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromSeconds(2));
            var oldBack = oldDir.Replace('/', '\\');
            text = Regex.Replace(text, $@"(?<![\w./\\-]){Regex.Escape(oldBack)}(?=[\\""'\s]|$)", newDir.Replace('/', '\\'), RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromSeconds(2));
        }

        return text;
    }

    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == ".." && parts.Count > 0)
                parts.RemoveAt(parts.Count - 1);
            else
                parts.Add(segment);
        }

        return string.Join('/', parts);
    }
}
