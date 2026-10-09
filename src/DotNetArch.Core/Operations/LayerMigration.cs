using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>One file that belongs in an ABP layer project: where it is now and where it goes (the path inside the project is kept).</summary>
internal sealed record LayerMove(string From, string To, string SourceProject);

/// <summary>What a layer project would receive, and what had to stay behind (with the reason), as found by <see cref="LayerMigration.Analyze"/>.</summary>
internal sealed record LayerAnalysis(
    IReadOnlyDictionary<ProjectLayer, IReadOnlyList<LayerMove>> Moves,
    IReadOnlyList<string> Manual);

/// <summary>
/// Content-driven ABP layers (D-27): a layer project exists only where there is something for it. The analysis finds the files that belong in
/// <c>Domain.Shared</c> (enum-only files), <c>Application.Contracts</c> (DTOs and MediatR requests) and <c>HttpApi</c> (controllers), pulls in the
/// self-contained types they need and leaves behind every file whose dependencies cannot move with it. Files keep their namespace and their path
/// inside the project; no code is edited.
/// </summary>
internal static partial class LayerMigration
{
    public static readonly ProjectLayer[] Targets = { ProjectLayer.DomainShared, ProjectLayer.ApplicationContracts, ProjectLayer.HttpApi };

    private sealed class SourceFile
    {
        public required string Path { get; init; }

        public required ProjectInfo Project { get; init; }

        public required string Text { get; init; }

        public required HashSet<string> Declared { get; init; }

        public required List<string> Kinds { get; init; }

        public HashSet<string> Identifiers { get; init; } = new(StringComparer.Ordinal);

        public bool Placed { get; init; }
    }

    public static LayerAnalysis Analyze(RepoContext ctx)
    {
        var files = Load(ctx);
        var index = new Dictionary<string, List<SourceFile>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var name in file.Declared)
            {
                if (!index.TryGetValue(name, out var list))
                    index[name] = list = new List<SourceFile>();
                list.Add(file);
            }
        }

        var target = new Dictionary<SourceFile, ProjectLayer>();
        foreach (var file in files)
        {
            if (file.Placed)
                target[file] = file.Project.Layer;
        }

        var initial = new Dictionary<SourceFile, ProjectLayer>();
        foreach (var file in files.Where(f => !f.Placed))
        {
            var layer = Classify(file);
            if (layer != null)
            {
                target[file] = layer.Value;
                initial[file] = layer.Value;
            }
        }

        // the parts of a partial type travel together: a controller's extra partial file (an added action) goes where the controller goes
        foreach (var file in files.Where(f => !f.Placed && !target.ContainsKey(f)))
        {
            var names = PartialType().Matches(file.Text).Select(m => m.Groups[1].Value).ToList();
            if (names.Count == 0)
                continue;
            var mate = files.FirstOrDefault(other => other != file && target.ContainsKey(other)
                && PartialType().Matches(other.Text).Any(m => names.Contains(m.Groups[1].Value)));
            if (mate != null)
            {
                target[file] = target[mate];
                initial[file] = target[mate];
            }
        }

        var rejected = new Dictionary<SourceFile, string>();
        string? togetherNote = null;
        bool changed;
        do
        {
            changed = false;
            foreach (var (file, layer) in target.ToList().Where(t => !t.Key.Placed))
            {
                foreach (var dependency in Dependencies(file, index))
                {
                    if (target.TryGetValue(dependency, out var placed) && Rank(placed) <= Rank(layer))
                        continue;
                    if (target.ContainsKey(dependency) || rejected.ContainsKey(dependency) || dependency.Placed)
                        continue;
                    var pull = PullTarget(dependency, layer);
                    if (pull != null)
                    {
                        target[dependency] = pull.Value;
                        changed = true;
                    }
                }
            }

            foreach (var (file, layer) in target.ToList().Where(t => !t.Key.Placed))
            {
                var offender = Dependencies(file, index).FirstOrDefault(d => !target.TryGetValue(d, out var placed) || Rank(placed) > Rank(layer));
                if (offender == null)
                    continue;
                target.Remove(file);
                rejected[file] = $"{file.Path} stays: it needs {offender.Path}, which cannot move to {Suffix(layer)}";
                changed = true;
            }
        }
        while (changed);

        // controllers move together: a host that is split over two assemblies breaks route discovery, so one blocked controller keeps all of them
        var blocked = rejected.Where(r => initial.TryGetValue(r.Key, out var l) && l == ProjectLayer.HttpApi && ControllerBase().IsMatch(r.Key.Text)).Select(r => r.Key).ToList();
        if (blocked.Count > 0)
        {
            var waiting = 0;
            foreach (var file in target.Where(t => !t.Key.Placed && t.Value == ProjectLayer.HttpApi).Select(t => t.Key).ToList())
            {
                target.Remove(file);
                if (initial.ContainsKey(file) && ControllerBase().IsMatch(file.Text))
                    waiting++;
            }

            togetherNote = waiting > 0 ? $"{waiting} controller(s) wait for the blocked one: controllers move together, because a host split over two assemblies breaks route discovery" : null;
        }

        // a type pulled in for a file that has since stayed behind has no reason to move
        var reach = new HashSet<SourceFile>(target.Keys.Where(f => f.Placed || initial.ContainsKey(f)));
        var queue = new Queue<SourceFile>(reach);
        while (queue.Count > 0)
        {
            foreach (var dependency in Dependencies(queue.Dequeue(), index))
            {
                if (target.ContainsKey(dependency) && reach.Add(dependency))
                    queue.Enqueue(dependency);
            }
        }

        foreach (var orphan in target.Keys.Where(f => !reach.Contains(f)).ToList())
            target.Remove(orphan);

        var moves = new Dictionary<ProjectLayer, List<LayerMove>>();
        var manual = new List<string>();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, layer) in target.Where(t => !t.Key.Placed).OrderBy(t => t.Key.Path, StringComparer.Ordinal))
        {
            var existing = ctx.OfLayer(layer).FirstOrDefault();
            var directory = existing?.Dir ?? NewProjectDir(ctx, layer);
            if (directory == null)
                continue;
            var inside = file.Path[(file.Project.Dir.Length + 1)..];
            var destination = $"{directory}/{inside}";
            if (!destinations.Add(destination) || ctx.Has(destination))
            {
                manual.Add($"{file.Path} stays: {destination} already exists");
                continue;
            }

            if (!moves.TryGetValue(layer, out var list))
                moves[layer] = list = new List<LayerMove>();
            list.Add(new LayerMove(file.Path, destination, file.Project.Name));
        }

        manual.AddRange(rejected.Where(r => initial.ContainsKey(r.Key)).Select(r => r.Value).OrderBy(m => m, StringComparer.Ordinal));
        if (togetherNote != null)
            manual.Add(togetherNote);
        return new LayerAnalysis(moves.ToDictionary(m => m.Key, m => (IReadOnlyList<LayerMove>)m.Value), manual);
    }

    /// <summary>Plans the fix: the new projects, the file moves, the references between layers and the restore lines of the Dockerfile.</summary>
    public static IReadOnlyList<FixAction> Plan(string root, RepoContext ctx, bool allowEmpty, out IReadOnlyList<string> manual, ProjectLayer? emptyOnly = null)
    {
        var analysis = Analyze(ctx);
        manual = analysis.Manual;
        var domain = ctx.OfLayer(ProjectLayer.Domain).FirstOrDefault();
        if (domain == null)
            return Array.Empty<FixAction>();

        var prefix = domain.Name[..^".Domain".Length];
        var actions = new List<FixAction>();
        var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        var framework = XDocument.Load(Path.Combine(root, domain.File)).Descendants("TargetFramework").FirstOrDefault()?.Value ?? "net8.0";

        var layerDirs = new Dictionary<ProjectLayer, string>();
        foreach (var layer in Targets.Append(ProjectLayer.HttpApiClient))
        {
            var existing = ctx.OfLayer(layer).FirstOrDefault();
            if (existing != null)
                layerDirs[layer] = existing.Dir;
        }

        var created = new List<ProjectLayer>();
        foreach (var layer in Targets.Append(ProjectLayer.HttpApiClient))
        {
            var receives = analysis.Moves.TryGetValue(layer, out var list) && list.Count > 0;
            if (layerDirs.ContainsKey(layer) || (!receives && !allowEmpty && emptyOnly != layer))
                continue;
            layerDirs[layer] = NewProjectDir(ctx, layer)!;
            created.Add(layer);
        }

        foreach (var layer in created)
        {
            var name = $"{prefix}.{Suffix(layer)}";
            var moved = analysis.Moves.TryGetValue(layer, out var list) ? list : Array.Empty<LayerMove>();
            var path = $"{layerDirs[layer]}/{name}.csproj";
            actions.Add(new FixAction(new PlannedChange(path, "create", $"ABP layer project {Suffix(layer)}" + (moved.Count == 0 ? " (empty, requested)" : $" ({moved.Count} file(s) move in)"), "DA-A02"),
                Csproj(root, ctx, prefix, layer, layerDirs, framework, moved), solution == null ? null : () => RegisterInSolution(root, solution, path)));
        }

        foreach (var (layer, moved) in analysis.Moves.OrderBy(m => Rank(m.Key)))
        {
            foreach (var move in moved)
                actions.Add(new FixAction(new PlannedChange(move.To, "move", $"{Suffix(layer)}: moved from {move.From}", "DA-A02"), string.Empty, MoveFrom: move.From));
        }

        actions.AddRange(EnsureProjectItems(root, ctx, analysis.Moves, created));
        actions.AddRange(ReferenceEdits(root, ctx, prefix, layerDirs, analysis));
        actions.AddRange(DockerfileEdits(ctx, prefix, layerDirs, created));
        return actions;
    }

    private static string Csproj(string root, RepoContext ctx, string prefix, ProjectLayer layer, IReadOnlyDictionary<ProjectLayer, string> dirs, string framework, IReadOnlyList<LayerMove> moved)
    {
        var dir = dirs[layer];
        var sb = new StringBuilder();
        sb.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>").Append(framework).Append("</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n");

        var inner = ReferenceTargets(layer, dirs);
        if (inner.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var other in inner)
                sb.Append("    <ProjectReference Include=\"").Append(RelativeProject(dir, prefix, other, dirs)).Append("\" />\n");
            sb.Append("  </ItemGroup>\n");
        }

        var items = ProjectItems(root, ctx, layer, moved);
        if (items.Web)
            sb.Append("\n  <ItemGroup>\n    <FrameworkReference Include=\"Microsoft.AspNetCore.App\" />\n  </ItemGroup>\n\n  <ItemGroup>\n    <Using Include=\"Microsoft.AspNetCore.Http\" />\n  </ItemGroup>\n");
        if (items.Packages.Count > 0)
            sb.Append("\n  <ItemGroup>\n").Append(string.Concat(items.Packages.Select(p => "    " + p + "\n"))).Append("  </ItemGroup>\n");
        if (items.Visible.Count > 0)
            sb.Append("\n  <ItemGroup>\n").Append(string.Concat(items.Visible.Select(v => "    " + v + "\n"))).Append("  </ItemGroup>\n");

        sb.Append("\n</Project>\n");
        return sb.ToString();
    }

    /// <summary>What the moved files need from their project: ASP.NET Core, the packages their usings name, and visibility of their old project's internals.</summary>
    private static (bool Web, List<string> Packages, List<string> Visible) ProjectItems(string root, RepoContext ctx, ProjectLayer layer, IReadOnlyList<LayerMove> moved)
    {
        var usings = moved.SelectMany(m => UsingNamespaces(ctx.Read(m.From))).ToHashSet(StringComparer.Ordinal);
        var web = usings.Any(u => u.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)) || layer == ProjectLayer.HttpApi;
        var sources = moved.Select(m => ctx.Projects.First(p => p.Name == m.SourceProject)).DistinctBy(p => p.Name).ToList();
        var packages = new List<string>();
        var visible = new List<string>();
        foreach (var source in sources)
        {
            var document = XDocument.Load(Path.Combine(root, source.File));
            foreach (var reference in document.Descendants("PackageReference"))
            {
                var id = (string?)reference.Attribute("Include");
                if (id == null || id.StartsWith("Corevia.Kit.", StringComparison.Ordinal) || !usings.Any(u => u == id || u.StartsWith(id + ".", StringComparison.Ordinal)))
                    continue;
                var text = reference.ToString(SaveOptions.DisableFormatting);
                if (!packages.Contains(text))
                    packages.Add(text);
            }

            // the old project keeps using the internals that moved out, and so do its tests
            var own = $"<InternalsVisibleTo Include=\"{source.Name}\" />";
            if (!visible.Contains(own))
                visible.Add(own);
            foreach (var entry in document.Descendants("InternalsVisibleTo").Select(e => e.ToString(SaveOptions.DisableFormatting)))
            {
                if (!visible.Contains(entry))
                    visible.Add(entry);
            }
        }

        return (web, packages, visible);
    }

    /// <summary>An existing layer project that receives files gets the packages, framework reference and internals visibility those files need.</summary>
    private static IEnumerable<FixAction> EnsureProjectItems(string root, RepoContext ctx, IReadOnlyDictionary<ProjectLayer, IReadOnlyList<LayerMove>> moves, IReadOnlyCollection<ProjectLayer> created)
    {
        foreach (var (layer, moved) in moves)
        {
            var project = ctx.OfLayer(layer).FirstOrDefault();
            if (project == null || created.Contains(layer) || moved.Count == 0)
                continue;
            var text = ctx.Read(project.File);
            var items = ProjectItems(root, ctx, layer, moved);
            var additions = new List<string>();
            if (items.Web && !text.Contains("Microsoft.AspNetCore.App", StringComparison.Ordinal))
                additions.Add("<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />");
            additions.AddRange(items.Packages.Where(p => !text.Contains(PackageId(p), StringComparison.Ordinal)));
            additions.AddRange(items.Visible);
            additions = additions.Distinct().ToList();
            var fresh = additions.Where(a => !text.Contains(a, StringComparison.Ordinal)).ToList();
            if (fresh.Count == 0)
                continue;
            var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
            if (close < 0)
                continue;
            var group = "  <ItemGroup>\n" + string.Concat(fresh.Select(a => "    " + a + "\n")) + "  </ItemGroup>\n";
            yield return new FixAction(new PlannedChange(project.File, "modify", "packages, framework reference and internals visibility for the moved files", "DA-A02"), text[..close].TrimEnd('\n') + "\n\n" + group + "\n" + text[close..]);
        }
    }

    private static string PackageId(string element) => Regex.Match(element, "Include=\"([^\"]+)\"").Groups[1].Value;

    private static List<ProjectLayer> ReferenceTargets(ProjectLayer layer, IReadOnlyDictionary<ProjectLayer, string> dirs) => layer switch
    {
        ProjectLayer.ApplicationContracts => dirs.ContainsKey(ProjectLayer.DomainShared) ? new List<ProjectLayer> { ProjectLayer.DomainShared } : new List<ProjectLayer>(),
        ProjectLayer.HttpApi or ProjectLayer.HttpApiClient => dirs.ContainsKey(ProjectLayer.ApplicationContracts) ? new List<ProjectLayer> { ProjectLayer.ApplicationContracts }
            : dirs.ContainsKey(ProjectLayer.DomainShared) ? new List<ProjectLayer> { ProjectLayer.DomainShared } : new List<ProjectLayer>(),
        _ => new List<ProjectLayer>(),
    };

    private static string RelativeProject(string fromDir, string prefix, ProjectLayer layer, IReadOnlyDictionary<ProjectLayer, string> dirs)
    {
        var name = $"{prefix}.{Suffix(layer)}";
        var target = $"{dirs[layer]}/{name}.csproj";
        return Path.GetRelativePath(fromDir, target).Replace('/', '\\');
    }

    /// <summary>Existing projects reference the layer they now depend on: Domain to Domain.Shared, Application to Application.Contracts, the host to HttpApi.</summary>
    private static IEnumerable<FixAction> ReferenceEdits(string root, RepoContext ctx, string prefix, IReadOnlyDictionary<ProjectLayer, string> dirs, LayerAnalysis analysis)
    {
        var edits = new List<(ProjectLayer From, ProjectLayer To)>();
        if (analysis.Moves.ContainsKey(ProjectLayer.DomainShared))
            edits.Add((ProjectLayer.Domain, ProjectLayer.DomainShared));
        if (analysis.Moves.ContainsKey(ProjectLayer.ApplicationContracts))
            edits.Add((ProjectLayer.Application, ProjectLayer.ApplicationContracts));
        if (analysis.Moves.ContainsKey(ProjectLayer.HttpApi))
            edits.Add((ProjectLayer.Api, ProjectLayer.HttpApi));

        foreach (var (from, to) in edits)
        {
            foreach (var project in ctx.OfLayer(from))
            {
                var include = RelativeProject(project.Dir, prefix, to, dirs);
                var text = ctx.Read(project.File);
                if (text.Length == 0 || text.Contains(include, StringComparison.OrdinalIgnoreCase))
                    continue;
                var line = $"    <ProjectReference Include=\"{include}\" />\n";
                var index = text.IndexOf("<ProjectReference", StringComparison.Ordinal);
                string updated;
                if (index >= 0)
                {
                    var start = text.LastIndexOf('\n', index) + 1;
                    updated = text[..start] + line + text[start..];
                }
                else
                {
                    var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
                    if (close < 0)
                        continue;
                    updated = text[..close].TrimEnd('\n') + "\n\n  <ItemGroup>\n" + line + "  </ItemGroup>\n" + text[close..];
                }

                yield return new FixAction(new PlannedChange(project.File, "modify", $"references {Suffix(to)}", "DA-A02"), updated);
            }
        }
    }

    /// <summary>A Dockerfile that restores per project lists the new projects next to the one they were split from.</summary>
    private static IEnumerable<FixAction> DockerfileEdits(RepoContext ctx, string prefix, IReadOnlyDictionary<ProjectLayer, string> dirs, IReadOnlyList<ProjectLayer> created)
    {
        var anchors = new Dictionary<ProjectLayer, ProjectLayer>
        {
            [ProjectLayer.DomainShared] = ProjectLayer.Domain,
            [ProjectLayer.ApplicationContracts] = ProjectLayer.Application,
            [ProjectLayer.HttpApi] = ProjectLayer.Api,
        };
        foreach (var dockerfile in ctx.Files.Where(f => Path.GetFileName(f).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)))
        {
            var text = ctx.Read(dockerfile);
            var lines = text.Split('\n').ToList();
            var changed = false;
            foreach (var layer in created.Where(anchors.ContainsKey))
            {
                var anchor = ctx.OfLayer(anchors[layer]).FirstOrDefault();
                if (anchor == null)
                    continue;
                var index = lines.FindIndex(l => l.StartsWith("COPY ", StringComparison.Ordinal) && l.Contains($"{anchor.Name}.csproj", StringComparison.Ordinal));
                var name = $"{prefix}.{Suffix(layer)}";
                if (index < 0 || lines.Any(l => l.Contains($"{name}.csproj", StringComparison.Ordinal)))
                    continue;
                lines.Insert(index + 1, lines[index].Replace(anchor.Dir, "\u0001D", StringComparison.Ordinal).Replace(anchor.Name, "\u0001N", StringComparison.Ordinal).Replace("\u0001D", dirs[layer], StringComparison.Ordinal).Replace("\u0001N", name, StringComparison.Ordinal));
                changed = true;
            }

            if (changed)
                yield return new FixAction(new PlannedChange(dockerfile, "modify", "restore stage lists the new layer projects", "DA-A02"), string.Join('\n', lines));
        }
    }

    private static void RegisterInSolution(string root, string solution, string project)
    {
        var result = Hosting.ToolHost.Runner.Run(new Hosting.ProcessSpec("dotnet", new[] { "sln", solution, "add", project }, root), showProgress: false);
        if (!result.Success)
            throw new InvalidOperationException($"dotnet sln add failed: {result.Output}");
    }

    private static string? NewProjectDir(RepoContext ctx, ProjectLayer layer)
    {
        var domain = ctx.OfLayer(ProjectLayer.Domain).FirstOrDefault();
        if (domain == null)
            return null;
        var prefix = domain.Name[..^".Domain".Length];
        var folder = domain.Dir.Contains('/') ? domain.Dir[..domain.Dir.LastIndexOf('/')] : string.Empty;
        var name = $"{prefix}.{Suffix(layer)}";
        return folder.Length == 0 ? name : $"{folder}/{name}";
    }

    public static string Suffix(ProjectLayer layer) => layer switch
    {
        ProjectLayer.DomainShared => "Domain.Shared",
        ProjectLayer.ApplicationContracts => "Application.Contracts",
        ProjectLayer.HttpApi => "HttpApi",
        ProjectLayer.HttpApiClient => "HttpApi.Client",
        _ => layer.ToString(),
    };

    private static int Rank(ProjectLayer layer) => layer switch
    {
        ProjectLayer.DomainShared => 0,
        ProjectLayer.ApplicationContracts => 1,
        ProjectLayer.HttpApi or ProjectLayer.HttpApiClient => 2,
        _ => 9,
    };

    private static bool UsesAspNet(SourceFile file) => file.Text.Contains("using Microsoft.AspNetCore", StringComparison.Ordinal);

    private static ProjectLayer? Classify(SourceFile file)
    {
        var layer = file.Project.Layer;
        if (layer is ProjectLayer.Domain or ProjectLayer.Application && file.Kinds.Count > 0 && file.Kinds.All(k => k == "enum"))
            return ProjectLayer.DomainShared;
        if (layer == ProjectLayer.Application && (file.Declared.Any(d => d.EndsWith("Dto", StringComparison.Ordinal)) || file.Path.Contains("/Dtos/", StringComparison.Ordinal) || RequestBase().IsMatch(file.Text)))
            return ProjectLayer.ApplicationContracts;
        if (layer == ProjectLayer.Api && ControllerBase().IsMatch(file.Text))
            return ProjectLayer.HttpApi;
        // the host composes; request-scoped services it keeps next to the controllers belong to the HTTP layer
        if (layer == ProjectLayer.Api && file.Path.Contains("/Services/", StringComparison.Ordinal) && file.Kinds.Count > 0)
            return ProjectLayer.HttpApi;
        // request/response models are the input and output contracts of the API: they belong to Application.Contracts so a typed client can use them
        if (layer is ProjectLayer.Api or ProjectLayer.HttpApi && ModelFolder().IsMatch(file.Path) && !UsesAspNet(file))
            return ProjectLayer.ApplicationContracts;
        return null;
    }

    /// <summary>The layer a dependency can be pulled into so that the file that needs it can move: Domain types only to Domain.Shared, Application types to Contracts, host types to HttpApi.</summary>
    private static ProjectLayer? PullTarget(SourceFile dependency, ProjectLayer needer)
    {
        var shape = dependency.Kinds.Count > 0 && dependency.Kinds.All(k => k is "enum" or "record" or "struct");
        ProjectLayer? wanted = dependency.Project.Layer switch
        {
            // only data shapes (enums, records, structs) leave the Domain; entities and services stay
            ProjectLayer.Domain => shape ? ProjectLayer.DomainShared : (ProjectLayer?)null,
            ProjectLayer.Application => needer != ProjectLayer.DomainShared && (shape || dependency.Declared.Any(d => d.EndsWith("Dto", StringComparison.Ordinal)) || dependency.Path.Contains("/Dtos/", StringComparison.Ordinal)) ? ProjectLayer.ApplicationContracts : (ProjectLayer?)null,
            // HTTP models live in conventional folders of the host; its services and implementations stay
            ProjectLayer.Api => ModelFolder().IsMatch(dependency.Path) ? (UsesAspNet(dependency) ? (needer == ProjectLayer.HttpApi ? ProjectLayer.HttpApi : (ProjectLayer?)null) : ProjectLayer.ApplicationContracts) : (ProjectLayer?)null,
            _ => (ProjectLayer?)null,
        };
        return wanted != null && Rank(wanted.Value) <= Rank(needer) ? wanted : null;
    }

    private static IEnumerable<SourceFile> Dependencies(SourceFile file, Dictionary<string, List<SourceFile>> index)
    {
        var result = new HashSet<SourceFile>();
        foreach (var identifier in file.Identifiers)
        {
            if (file.Declared.Contains(identifier) || !index.TryGetValue(identifier, out var declarers))
                continue;
            foreach (var declarer in declarers)
            {
                if (declarer != file)
                    result.Add(declarer);
            }
        }

        return result;
    }

    private static List<SourceFile> Load(RepoContext ctx)
    {
        var result = new List<SourceFile>();
        foreach (var project in ctx.Source)
        {
            foreach (var path in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                var text = ctx.Read(path);
                if (text.Length == 0)
                    continue;
                var stripped = Strip(text);
                var declared = new HashSet<string>(StringComparer.Ordinal);
                var kinds = new List<string>();
                foreach (Match match in Declaration().Matches(stripped))
                {
                    declared.Add(match.Groups["name"].Value);
                    kinds.Add(match.Groups["kind"].Value.StartsWith("record", StringComparison.Ordinal) ? "record" : match.Groups["kind"].Value);
                }

                var identifiers = new HashSet<string>(Identifier().Matches(WithoutMemberNames(stripped)).Select(m => m.Value), StringComparer.Ordinal);
                var placed = project.Layer is ProjectLayer.DomainShared or ProjectLayer.ApplicationContracts or ProjectLayer.HttpApi
                    && !(project.Layer == ProjectLayer.HttpApi && ModelFolder().IsMatch(path) && !stripped.Contains("using Microsoft.AspNetCore", StringComparison.Ordinal));
                result.Add(new SourceFile { Path = path, Project = project, Text = stripped, Declared = declared, Kinds = kinds, Identifiers = identifiers, Placed = placed });
            }
        }

        return result;
    }

    private static readonly HashSet<string> NonTypeKeywords = new(StringComparer.Ordinal)
    {
        "new", "typeof", "is", "as", "return", "throw", "case", "await", "yield", "using", "nameof", "default", "when", "in", "out", "ref", "else", "do", "goto",
    };

    /// <summary>
    /// Replaces the name of a member, parameter or variable (<c>string Unit,</c>, <c>Unit? Unit { get; }</c>) so that a property called like a type is
    /// not read as a use of that type. What follows a type is a name when it is followed by <c>, ; ) = {</c>, <c>=&gt;</c> or <c>(</c>.
    /// </summary>
    internal static string WithoutMemberNames(string text)
    {
        text = EnumBody().Replace(text, match => match.Groups["head"].Value + EnumMember().Replace(match.Groups["body"].Value, "_") + "}");
        text = MemberAccess().Replace(text, "._");
        text = NamedArgument().Replace(text, "_");
        text = InitializerMember().Replace(text, "_");
        return MemberName().Replace(text, match => NonTypeKeywords.Contains(match.Groups["prev"].Value) ? match.Value : match.Groups["prev"].Value + " _");
    }

    [GeneratedRegex(@"(?<head>\benum\s+\w+[^{]*\{)(?<body>[^}]*)\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex EnumBody();

    [GeneratedRegex(@"\b[A-Za-z_]\w*(?=\s*(?:=|,|$))", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex EnumMember();

    [GeneratedRegex(@"(?<=[(,]\s*)[A-Za-z_]\w*(?=\s*:(?!:))", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NamedArgument();

    [GeneratedRegex(@"(?<=[{,]\s*)[A-Za-z_]\w*(?=\s*=(?![=>]))", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex InitializerMember();

    [GeneratedRegex(@"(?<=\w\s*)\.\s*[A-Za-z_]\w*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MemberAccess();

    [GeneratedRegex(@"(?<prev>[A-Za-z_][\w.]*(?:<[^<>;{}]*(?:<[^<>;{}]*>[^<>;{}]*)*>)?[?\]]*)\s+(?<name>[A-Za-z_]\w*)(?=\s*(?:[,;)=]|\{|=>|\())", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MemberName();

    internal static string Strip(string text)
    {
        text = BlockComment().Replace(text, " ");
        text = LineComment().Replace(text, string.Empty);
        return StringLiteral().Replace(text, "\"\"");
    }

    private static IEnumerable<string> UsingNamespaces(string text) => UsingDirective().Matches(text).Select(m => m.Groups[1].Value);

    [GeneratedRegex(@"/(?:Contracts|Models|Dtos|Requests|Responses)/", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModelFolder();

    [GeneratedRegex(@"\b(?<kind>class|record\s+struct|record\s+class|record|struct|interface|enum)\s+(?<name>[A-Za-z_]\w*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\b[A-Z][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Identifier();

    [GeneratedRegex(@":\s*[^{;]*\bIRequest\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RequestBase();

    [GeneratedRegex(@":\s*(?:Microsoft\.AspNetCore\.Mvc\.)?(?:ControllerBase|Controller)\b|\[ApiController\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ControllerBase();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LineComment();

    [GeneratedRegex(@"""(?:[^""\\\n]|\\.)*""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\bpartial\s+(?:class|record|struct|interface)\s+([A-Za-z_]\w*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PartialType();

    [GeneratedRegex(@"(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?([A-Za-z_][\w.]*)\s*;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UsingDirective();
}
