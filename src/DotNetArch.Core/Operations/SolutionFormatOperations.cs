using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>migrate_solution_format</c>: replaces each classic <c>.sln</c> with the XML <c>.slnx</c> solution file (supported from .NET 9.0.200, the default from .NET 10).
/// Projects, solution folders and solution items are kept; the old file is removed on apply and the references to it in Dockerfiles, pipelines and scripts
/// are moved to the new name. A repository without a <c>.sln</c> is left alone, so the operation is safe to run again.
/// </summary>
internal static partial class SolutionFormatOperations
{
    private const string SolutionFolderType = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    public static IReadOnlyList<OperationDefinition> All => new[] { MigrateSolutionFormat };

    public static OperationDefinition MigrateSolutionFormat { get; } = new(
        "migrate_solution_format",
        "Replace every classic .sln solution with a .slnx file (projects, solution folders and solution items kept) and move the references to it in Dockerfiles, pipelines and scripts. For repositories on .NET 9.0.200 or later. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true, Default: "@cwd"),
        },
        request =>
        {
            var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
            if (!Directory.Exists(root))
                throw new ArgumentException($"Folder not found: {root}");

            var ctx = new RepoContext(root, NetArchStore.ResolveProfile(root, null, out _), new RuleSettings(), Enumerable.Empty<string>());
            var plan = Plan(ctx, out var manual);
            if (request.Apply)
                PlanApplier.Apply(root, plan);

            var changes = plan.Select(p => p.Change).ToList();
            var text = $"migrate_solution_format: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {changes.Count} change(s)\n"
                       + string.Join("\n", changes.Select(c => $"  {c.Action,-7} {c.Path}  {c.Reason}"))
                       + (manual.Count == 0 ? string.Empty : "\nmanual:\n" + string.Join("\n", manual.Select(m => "  " + m)));
            return new OperationResult(true, text, new { manual }, changes, request.Apply);
        });

    internal static IReadOnlyList<FixAction> Plan(RepoContext ctx, out List<string> manual)
    {
        manual = new List<string>();
        var actions = new List<FixAction>();
        var solutions = ctx.Files.Where(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToList();
        if (solutions.Count == 0)
        {
            manual.Add("Nothing to change: no .sln file was found.");
            return actions;
        }

        foreach (var sln in solutions)
        {
            var target = Path.ChangeExtension(sln, ".slnx");
            if (ctx.Has(target))
            {
                manual.Add($"{sln}: {target} already exists; remove one of the two by hand.");
                continue;
            }

            var absolute = Path.Combine(ctx.Root, sln);
            actions.Add(new FixAction(
                new PlannedChange(target, "create", $"replaces {sln} (the old file is removed)"),
                Convert(ctx.Read(sln)),
                () => File.Delete(absolute)));
        }

        foreach (var file in ctx.Files.Where(IsBuildFile))
        {
            var text = ctx.Read(file);
            var updated = SolutionReference().Replace(text, m => m.Value + "x");
            if (updated != text)
                actions.Add(new FixAction(new PlannedChange(file, "modify", "references to .sln move to .slnx"), updated));
        }

        manual.Add("Use dotnet 9.0.200 or later to build; commands that name the solution (`dotnet build X.sln`) now name the .slnx file. Run restore, build and the tests.");
        return actions;
    }

    /// <summary>Converts the text of a classic solution file to the .slnx XML form.</summary>
    internal static string Convert(string sln)
    {
        var projects = new Dictionary<string, (string Type, string Name, string Path, List<string> Items)>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var nested = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lines = sln.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var head = ProjectLine().Match(lines[i]);
            if (head.Success)
            {
                var id = head.Groups["id"].Value;
                var items = new List<string>();
                var j = i + 1;
                var inItems = false;
                for (; j < lines.Length && lines[j].Trim() != "EndProject"; j++)
                {
                    var line = lines[j].Trim();
                    if (line.StartsWith("ProjectSection(SolutionItems)", StringComparison.Ordinal)) { inItems = true; continue; }
                    if (line == "EndProjectSection") { inItems = false; continue; }
                    var item = SolutionItem().Match(lines[j]);
                    if (inItems && item.Success)
                        items.Add(item.Groups["path"].Value.Replace('\\', '/'));
                }

                projects[id] = (head.Groups["type"].Value, head.Groups["name"].Value, head.Groups["path"].Value.Replace('\\', '/'), items);
                order.Add(id);
                i = j;
                continue;
            }

            var pair = NestedPair().Match(lines[i]);
            if (pair.Success)
                nested[pair.Groups["child"].Value] = pair.Groups["parent"].Value;
        }

        string FolderPath(string id)
        {
            var parts = new List<string>();
            for (var current = nested.GetValueOrDefault(id); current != null; current = nested.GetValueOrDefault(current))
            {
                if (!projects.TryGetValue(current, out var folder)) break;
                parts.Insert(0, folder.Name);
            }

            return parts.Count == 0 ? string.Empty : "/" + string.Join("/", parts) + "/";
        }

        var root = new XElement("Solution");
        var folders = new Dictionary<string, XElement>(StringComparer.Ordinal);

        XElement Folder(string path)
        {
            if (folders.TryGetValue(path, out var existing)) return existing;
            // A nested folder is written flat with its full path (/src/domain/), as the format asks; the parents are created first so they exist too.
            var parentPath = path[..(path.TrimEnd('/').LastIndexOf('/') + 1)];
            if (parentPath.Length > 1) Folder(parentPath);
            var element = new XElement("Folder", new XAttribute("Name", path));
            root.Add(element);
            return folders[path] = element;
        }

        foreach (var id in order.Where(o => projects[o].Type.Equals(SolutionFolderType, StringComparison.OrdinalIgnoreCase)))
        {
            var parent = FolderPath(id);
            var folder = Folder((parent.Length == 0 ? "/" : parent) + projects[id].Name + "/");
            foreach (var item in projects[id].Items)
                folder.Add(new XElement("File", new XAttribute("Path", item)));
        }

        foreach (var id in order.Where(o => !projects[o].Type.Equals(SolutionFolderType, StringComparison.OrdinalIgnoreCase)))
        {
            var folder = FolderPath(id);
            var project = new XElement("Project", new XAttribute("Path", projects[id].Path));
            (folder.Length == 0 ? root : Folder(folder)).Add(project);
        }

        return new XDocument(root).ToString(SaveOptions.None).Replace("\r\n", "\n") + "\n";
    }

    private static bool IsBuildFile(string file)
    {
        var name = Path.GetFileName(file);
        var lower = file.ToLowerInvariant();
        return name.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".dockerfile", StringComparison.OrdinalIgnoreCase)
               || lower.EndsWith(".yml", StringComparison.Ordinal)
               || lower.EndsWith(".yaml", StringComparison.Ordinal)
               || lower.EndsWith(".sh", StringComparison.Ordinal)
               || lower.EndsWith(".props", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^Project\(""\{(?<type>[^}]+)\}""\)\s*=\s*""(?<name>[^""]*)"",\s*""(?<path>[^""]*)"",\s*""\{(?<id>[^}]+)\}""")]
    private static partial Regex ProjectLine();

    [GeneratedRegex(@"^\s*(?<path>[^=\s][^=]*?)\s*=\s*[^=]+$")]
    private static partial Regex SolutionItem();

    [GeneratedRegex(@"^\s*\{(?<child>[0-9A-Fa-f-]+)\}\s*=\s*\{(?<parent>[0-9A-Fa-f-]+)\}\s*$")]
    private static partial Regex NestedPair();

    [GeneratedRegex(@"\.sln\b(?!x)")]
    private static partial Regex SolutionReference();
}
