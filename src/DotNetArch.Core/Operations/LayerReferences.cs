using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// References between the projects of a solution (DA-A11): every project a file uses is reachable through project references that respect the
/// layer direction, a reference the direction forbids and nothing uses is removed, every project is in the solution, and a Dockerfile that restores
/// per project lists every project it needs. Only references, the solution and the Dockerfile are edited; no source file is.
/// </summary>
internal static partial class LayerReferences
{
    public sealed record Issue(string Kind, string Project, string Target, string Detail);

    public static IReadOnlyList<Issue> Issues(RepoContext ctx)
    {
        var issues = new List<Issue>();
        var projects = ctx.Projects.ToList();
        var byName = projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // type name -> the one source project declaring it (with its namespace); names declared twice are ignored
        var declared = new Dictionary<string, List<(ProjectInfo Project, string Namespace)>>(StringComparer.Ordinal);
        var usage = new List<(ProjectInfo Project, string File, HashSet<string> Identifiers, HashSet<string> Usings, string Namespace)>();
        foreach (var project in projects.Where(p => !p.IsTest))
        {
            foreach (var file in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var text = LayerMigration.Strip(ctx.Read(file));
                if (text.Length == 0)
                    continue;
                var space = Namespace().Match(text).Groups[1].Value;
                foreach (Match match in Declaration().Matches(text))
                {
                    var name = match.Groups["name"].Value;
                    if (!declared.TryGetValue(name, out var list))
                        declared[name] = list = new List<(ProjectInfo, string)>();
                    list.Add((project, space));
                }

                usage.Add((project, file, new HashSet<string>(Identifier().Matches(LayerMigration.WithoutMemberNames(text)).Select(m => m.Value), StringComparer.Ordinal), new HashSet<string>(Using().Matches(text).Select(m => m.Groups[1].Value), StringComparer.Ordinal), space));
            }
        }

        var closure = projects.ToDictionary(p => p.Name, p => Closure(p, byName), StringComparer.OrdinalIgnoreCase);

        // 1. used but not reachable
        var reachMissing = new HashSet<(string, string)>();
        foreach (var (project, file, identifiers, usings, space) in usage)
        {
            foreach (var identifier in identifiers)
            {
                if (!declared.TryGetValue(identifier, out var owners) || owners.Count != 1)
                    continue;
                var (owner, ownerSpace) = owners[0];
                if (owner.Name == project.Name || closure[project.Name].Contains(owner.Name))
                    continue;
                var visible = ownerSpace.Length == 0 || usings.Contains(ownerSpace) || space == ownerSpace || space.StartsWith(ownerSpace + ".", StringComparison.Ordinal);
                if (visible && reachMissing.Add((project.Name, owner.Name)))
                    issues.Add(new Issue("missing", project.Name, owner.Name, $"{file} uses {identifier}"));
            }
        }

        // 2. forbidden and unused
        foreach (var project in projects.Where(p => !p.IsTest))
        {
            foreach (var reference in project.ProjectReferences)
            {
                if (!byName.TryGetValue(Path.GetFileNameWithoutExtension(reference), out var target) || target.IsTest)
                    continue;
                if (Allowed(project.Layer, target.Layer))
                    continue;
                var used = usage.Any(u => u.Project.Name == project.Name && u.Identifiers.Any(i => declared.TryGetValue(i, out var owners) && owners.Any(o => o.Project.Name == target.Name)));
                issues.Add(new Issue(used ? "forbidden-used" : "forbidden", project.Name, target.Name, used ? "used by the project's code: move the type to the right layer" : "nothing uses it"));
            }
        }

        // 3. solution
        var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        if (solution != null)
        {
            var text = ctx.Read(solution);
            foreach (var project in projects.Where(p => !text.Contains(Path.GetFileName(p.File), StringComparison.OrdinalIgnoreCase)))
                issues.Add(new Issue("solution", project.Name, Path.GetFileName(solution), "not in the solution"));
        }

        // 4. Dockerfile restore lines
        foreach (var dockerfile in ctx.Files.Where(f => Path.GetFileName(f).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)))
        {
            var copies = ctx.Read(dockerfile).Split('\n').Where(l => l.StartsWith("COPY ", StringComparison.Ordinal) && l.Contains(".csproj", StringComparison.Ordinal)).ToList();
            var listed = projects.Where(p => copies.Any(c => c.Contains(Path.GetFileName(p.File), StringComparison.Ordinal))).ToList();
            foreach (var needed in listed.SelectMany(p => closure[p.Name]).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (byName.TryGetValue(needed, out var project) && !project.IsTest && listed.All(l => l.Name != needed))
                    issues.Add(new Issue("dockerfile", project.Name, dockerfile, "the restore stage does not copy its project file"));
            }
        }

        return issues;
    }

    public static IReadOnlyList<FixAction> Plan(string root, RepoContext ctx, out IReadOnlyList<string> manual)
    {
        var notes = new List<string>();
        manual = notes;
        var actions = new List<FixAction>();
        var issues = Issues(ctx);
        var byName = ctx.Projects.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group in issues.Where(i => i.Kind is "missing" or "forbidden").GroupBy(i => i.Project))
        {
            var project = byName[group.Key];
            var text = ctx.Read(project.File);
            var changed = text;
            foreach (var issue in group)
            {
                var target = byName[issue.Target];
                if (issue.Kind == "forbidden")
                {
                    changed = Regex.Replace(changed, $@"[ \t]*<ProjectReference\s+Include=""[^""]*{Regex.Escape(target.Name)}\.csproj""\s*/>[ \t]*\r?\n?", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                    continue;
                }

                if (!Allowed(project.Layer, target.Layer))
                {
                    notes.Add($"DA-A11  {project.Name} uses {target.Name} ({issue.Detail}) but its layer may not reference it: move the type to a layer both may see");
                    continue;
                }

                changed = AddReference(changed, Path.GetRelativePath(project.Dir, target.File).Replace('/', '\\'));
            }

            if (changed != text)
                actions.Add(new FixAction(new PlannedChange(project.File, "modify", "project references follow the layer direction", "DA-A11"), changed));
        }

        notes.AddRange(issues.Where(i => i.Kind == "forbidden-used").Select(i => $"DA-A11  {i.Project} -> {i.Target}: {i.Detail}"));

        var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        foreach (var issue in issues.Where(i => i.Kind == "solution"))
        {
            var project = byName[issue.Project];
            actions.Add(new FixAction(new PlannedChange(solution!, "modify", $"adds {project.Name} to the solution", "DA-A11"), ctx.Read(solution!), () => Register(root, solution!, project.File)));
        }

        foreach (var group in issues.Where(i => i.Kind == "dockerfile").GroupBy(i => i.Target))
        {
            var lines = ctx.Read(group.Key).Split('\n').ToList();
            foreach (var issue in group)
            {
                var project = byName[issue.Project];
                var anchorIndex = lines.FindLastIndex(l => l.StartsWith("COPY ", StringComparison.Ordinal) && l.Contains(".csproj", StringComparison.Ordinal));
                var anchor = ctx.Projects.FirstOrDefault(p => lines[anchorIndex].Contains(Path.GetFileName(p.File), StringComparison.Ordinal));
                if (anchorIndex < 0 || anchor == null)
                    continue;
                lines.Insert(anchorIndex + 1, lines[anchorIndex].Replace(anchor.Dir, "\u0001D", StringComparison.Ordinal).Replace(anchor.Name, "\u0001N", StringComparison.Ordinal).Replace("\u0001D", project.Dir, StringComparison.Ordinal).Replace("\u0001N", project.Name, StringComparison.Ordinal));
            }

            actions.Add(new FixAction(new PlannedChange(group.Key, "modify", "restore stage copies every project file it needs", "DA-A11"), string.Join('\n', lines)));
        }

        return actions;
    }

    private static string AddReference(string text, string include)
    {
        if (text.Contains(include, StringComparison.OrdinalIgnoreCase))
            return text;
        var line = $"    <ProjectReference Include=\"{include}\" />\n";
        var index = text.IndexOf("<ProjectReference", StringComparison.Ordinal);
        if (index >= 0)
        {
            var start = text.LastIndexOf('\n', index) + 1;
            return text[..start] + line + text[start..];
        }

        var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        return close < 0 ? text : text[..close].TrimEnd('\n') + "\n\n  <ItemGroup>\n" + line + "  </ItemGroup>\n" + text[close..];
    }

    private static HashSet<string> Closure(ProjectInfo project, Dictionary<string, ProjectInfo> byName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<ProjectInfo>();
        queue.Enqueue(project);
        while (queue.Count > 0)
        {
            foreach (var reference in queue.Dequeue().ProjectReferences)
            {
                var name = Path.GetFileNameWithoutExtension(reference);
                if (byName.TryGetValue(name, out var next) && result.Add(name))
                    queue.Enqueue(next);
            }
        }

        return result;
    }

    // layers a layer may reference: the clean-architecture rules and, when present, the ABP package rules
    internal static bool Allowed(ProjectLayer from, ProjectLayer to)
    {
        if (from == to || from == ProjectLayer.Other || to == ProjectLayer.Other)
            return true;
        return Abp.TryGetValue(from, out var allowed) ? allowed.Contains(to) : StructureChecks.Permitted(from, to);
    }

    private static readonly Dictionary<ProjectLayer, ProjectLayer[]> Abp = new()
    {
        [ProjectLayer.DomainShared] = Array.Empty<ProjectLayer>(),
        [ProjectLayer.Domain] = new[] { ProjectLayer.DomainShared, ProjectLayer.Contracts },
        [ProjectLayer.ApplicationContracts] = new[] { ProjectLayer.DomainShared, ProjectLayer.Contracts },
        [ProjectLayer.HttpApi] = new[] { ProjectLayer.ApplicationContracts, ProjectLayer.DomainShared, ProjectLayer.Contracts },
        [ProjectLayer.HttpApiClient] = new[] { ProjectLayer.ApplicationContracts, ProjectLayer.DomainShared, ProjectLayer.Contracts },
    };

    private static void Register(string root, string solution, string project)
    {
        var result = Hosting.ToolHost.Runner.Run(new Hosting.ProcessSpec("dotnet", new[] { "sln", solution, "add", project }, root), showProgress: false);
        if (!result.Success)
            throw new InvalidOperationException($"dotnet sln add failed: {result.Output}");
    }

    [GeneratedRegex(@"(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?([A-Za-z_][\w.]*)\s*;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Using();

    [GeneratedRegex(@"(?m)^\s*namespace\s+([A-Za-z_][\w.]*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Namespace();

    [GeneratedRegex(@"\b(?<kind>class|record\s+struct|record\s+class|record|struct|interface|enum)\s+(?<name>[A-Za-z_]\w*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\b[A-Z][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Identifier();



}
