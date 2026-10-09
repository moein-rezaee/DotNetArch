using DotNetArch.Core.Doctor;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Operations that work on any existing repository (no <c>dotnet-arch.yml</c> needed): add a layer project, add the test project of a layer, manage spec
/// documents and describe the project graph. Same registry, same CLI and MCP surface as every other operation (D-33).
/// </summary>
internal static class ProjectOperations
{
    private static readonly string[] LayerNames = { "Domain.Shared", "Application.Contracts", "HttpApi", "HttpApi.Client" };

    private static readonly Dictionary<string, ProjectLayer> TestableLayers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Domain"] = ProjectLayer.Domain,
        ["Domain.Shared"] = ProjectLayer.DomainShared,
        ["Application"] = ProjectLayer.Application,
        ["Application.Contracts"] = ProjectLayer.ApplicationContracts,
        ["Infrastructure"] = ProjectLayer.Infrastructure,
        ["HttpApi"] = ProjectLayer.HttpApi,
        ["HttpApi.Client"] = ProjectLayer.HttpApiClient,
        ["Api"] = ProjectLayer.Api,
        ["Mcp"] = ProjectLayer.Mcp,
    };

    private static OperationParameter PathParam(bool positional = true) => new("path", "Repository root (default: current folder).", Positional: positional, Default: "@cwd");

    public static IReadOnlyList<OperationDefinition> All => new[] { AddLayer, AddTests, SpecList, SpecAdd, SpecCheck, Graph };

    private static string Root(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        if (!Directory.Exists(root))
            throw new ArgumentException($"Folder not found: {root}");
        return root;
    }

    private static RepoContext Context(string root)
    {
        var profile = NetArchStore.ResolveProfile(root, null, out _);
        var standards = NetArchStore.LoadState(root)?.Standards ?? new List<string>();
        return new RepoContext(root, profile, new RuleSettings(), standards.Append(AbpChecks.Standard).Distinct());
    }

    private static OperationResult Planned(string operation, string root, IReadOnlyList<FixAction> plan, bool apply, IReadOnlyList<string> manual)
    {
        if (apply)
            PlanApplier.Apply(root, plan);
        var changes = plan.Select(p => p.Change).ToList();
        var text = $"{operation}: {(apply ? "applied" : "plan (dry run, add --apply to write)")} - {changes.Count} change(s)\n"
                   + string.Join("\n", changes.Select(c => $"  {c.Action,-7} {c.Path}  {c.Reason}"))
                   + (manual.Count == 0 ? string.Empty : "\nmanual:\n" + string.Join("\n", manual.Select(m => "  " + m)));
        return new OperationResult(true, text, new { manual }, changes, apply);
    }

    // ------------------------------------------------------------------------------------------------------------------------------

    public static OperationDefinition AddLayer { get; } = new(
        "add_layer",
        "Add an ABP layer project (Domain.Shared, Application.Contracts, HttpApi, HttpApi.Client) by moving into it the files that belong there, with references, packages, Dockerfile and solution wired and namespaces unchanged. A layer exists only where something moves into it: when nothing belongs there nothing is created. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(positional: false),
            new OperationParameter("layer", "The layer project to add.", Required: true, Positional: true, Choices: LayerNames),
            new OperationParameter("empty", "Create the layer even though nothing moves into it (a planned development target).", ParameterType.Flag, CliOnly: true),
        },
        request =>
        {
            var root = Root(request);
            var layerName = LayerNames.FirstOrDefault(n => n.Equals(request.Get("layer"), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"Unknown layer. Use one of: {string.Join(", ", LayerNames)}.");
            var ctx = Context(root);
            var layer = TestableLayers[layerName];
            if (ctx.OfLayer(layer).Any())
                return new OperationResult(true, $"add_layer: {layerName} already exists (0 changes).", Plan: Array.Empty<PlannedChange>());

            var manual = new List<string>();
            IReadOnlyList<FixAction> plan;
            if (layer == ProjectLayer.HttpApiClient && !request.Flag("empty"))
            {
                plan = TypedClient.Plan(root, ctx, out var notes);
                manual.AddRange(notes);
            }
            else
            {
                plan = FixOperation.Structural("DA-A02", root, ctx, false, request.Flag("empty"), manual, request.Flag("empty") ? layer : null);
            }

            var creates = plan.Any(a => a.Change.Path.EndsWith($".{layerName}.csproj", StringComparison.OrdinalIgnoreCase));
            if (!creates)
                return new OperationResult(false, string.Empty, ExitCode: 1, Error: $"Nothing belongs in {layerName}, so it was not created. A layer exists only where something moves into it (the CLI can create an empty one with --empty).");
            return Planned("add_layer", root, plan, request.Apply, manual);
        });

    public static OperationDefinition AddTests { get; } = new(
        "add_tests",
        "Add the test project of a layer (<Project>.Tests in the test folder) with an architecture test and register it in the solution. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(positional: false),
            new OperationParameter("layer", "The layer to test.", Required: true, Positional: true, Choices: TestableLayers.Keys.ToArray()),
        },
        request =>
        {
            var root = Root(request);
            var layer = request.Get("layer")!;
            if (!TestableLayers.TryGetValue(layer, out var projectLayer))
                throw new ArgumentException($"Unknown layer '{layer}'. Use one of: {string.Join(", ", TestableLayers.Keys)}.");
            var ctx = Context(root);
            if (!ctx.OfLayer(projectLayer).Any())
                throw new ArgumentException($"The repository has no {layer} project to test.");
            if (ctx.OfLayer(projectLayer, tests: true).Any())
                return new OperationResult(true, $"add_tests: a test project for {layer} already exists (0 changes).", Plan: Array.Empty<PlannedChange>());
            return Planned("add_tests", root, StructuralFixers.LayerTests(root, ctx, projectLayer, ctx.Has("Directory.Packages.props"), "add_tests"), request.Apply, Array.Empty<string>());
        });

    // ------------------------------------------------------------------------------------------------------------------------------

    public static OperationDefinition SpecList { get; } = new(
        "spec_list",
        "List the spec documents under docs/specs with their Persian pair and the machine-readable spec files.",
        OperationKind.ReadOnly,
        new[] { PathParam() },
        request =>
        {
            var root = Root(request);
            var specs = SpecFiles(root);
            var text = specs.Count == 0 ? "spec_list: no spec documents under docs/specs." : string.Join("\n", specs.Select(s => $"{s.Name,-28} en={(s.English ? "yes" : "NO")} fa={(s.Persian ? "yes" : "NO")}"));
            return new OperationResult(true, text, new { specs });
        });

    public static OperationDefinition SpecAdd { get; } = new(
        "spec_add",
        "Add a spec document pair (docs/specs/<name>.md and <name>.fa.md) from a skeleton and list it in docs/INDEX.md and docs/INDEX.fa.md when they exist. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(),
            new OperationParameter("name", "Spec name: lowercase letters, digits and dashes.", Required: true),
            new OperationParameter("title", "Spec title (default: the name)."),
        },
        request =>
        {
            var root = Root(request);
            var name = request.Get("name")!;
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z][a-z0-9-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                throw new ArgumentException("A spec name uses lowercase letters, digits and dashes and starts with a letter.");
            var title = request.Get("title") ?? name;
            var plan = new List<FixAction>();
            foreach (var (suffix, language, content) in new[]
            {
                (".md", "en", $"[فارسی](./{name}.fa.md)\n\n# {title}\n\n## Purpose\n\n## Requirements\n\n## Acceptance\n"),
                (".fa.md", "fa", $"[English](./{name}.md)\n\n# {title}\n\n## هدف\n\n## الزامات\n\n## پذیرش\n"),
            })
            {
                var path = $"docs/specs/{name}{suffix}";
                if (!File.Exists(Path.Combine(root, path)))
                    plan.Add(new FixAction(new PlannedChange(path, "create", $"spec skeleton ({language})", "spec_add"), content));
            }

            foreach (var (index, line) in new[] { ("docs/INDEX.md", $"- [{title}](./specs/{name}.md)"), ("docs/INDEX.fa.md", $"- [{title}](./specs/{name}.fa.md)") })
            {
                var full = Path.Combine(root, index);
                if (File.Exists(full) && !File.ReadAllText(full).Contains($"specs/{name}", StringComparison.Ordinal))
                    plan.Add(new FixAction(new PlannedChange(index, "modify", "spec listed in the index", "spec_add"), File.ReadAllText(full).TrimEnd('\n') + "\n" + line + "\n"));
            }

            return Planned("spec_add", root, plan, request.Apply, Array.Empty<string>());
        });

    public static OperationDefinition SpecCheck { get; } = new(
        "spec_check",
        "Check the documentation and specs: README pairs, docs index, spec documents, Persian pairs, machine-readable spec files and the OpenAPI snapshot (the doctor's documentation rules only).",
        OperationKind.ReadOnly,
        new[] { PathParam() },
        request =>
        {
            var root = Root(request);
            var report = DoctorRunner.Run(root);
            var findings = report.Findings.Where(f => f.Id.StartsWith("DA-M", StringComparison.Ordinal)).ToList();
            var text = findings.Count == 0 ? "spec_check: documentation and specs are consistent." : string.Join("\n", findings.Select(f => $"{f.Severity,-7} {f.Id}  {f.Message}"));
            return new OperationResult(findings.All(f => f.Severity != DoctorSeverity.Error), text, new { findings }, ExitCode: findings.Any(f => f.Severity == DoctorSeverity.Error) ? 3 : 0);
        });

    private static List<SpecEntry> SpecFiles(string root)
    {
        var folder = Path.Combine(root, "docs", "specs");
        if (!Directory.Exists(folder))
            return new List<SpecEntry>();
        var names = Directory.EnumerateFiles(folder, "*.md").Select(Path.GetFileName).Select(f => f!.EndsWith(".fa.md", StringComparison.Ordinal) ? f[..^6] : f[..^3]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return names.Select(n => new SpecEntry(n, File.Exists(Path.Combine(folder, n + ".md")), File.Exists(Path.Combine(folder, n + ".fa.md")))).ToList();
    }

    private sealed record SpecEntry(string Name, bool English, bool Persian);

    // ------------------------------------------------------------------------------------------------------------------------------

    public static OperationDefinition Graph { get; } = new(
        "graph",
        "Describe the project graph of a repository as data: projects with their layer, project references, packages and kit usage, plus the layer summary. Read-only; with the CLI's --out it can be saved as a file for knowledge tooling.",
        OperationKind.ReadOnly,
        new[] { PathParam() },
        request =>
        {
            var root = Root(request);
            var ctx = Context(root);
            var nodes = ctx.Projects.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new { p.Name, layer = p.Layer.ToString(), test = p.IsTest, path = p.File }).ToList();
            var edges = ctx.Projects.SelectMany(p => p.ProjectReferences.Select(r => new { from = p.Name, to = Path.GetFileNameWithoutExtension(r), kind = "references" }))
                .Concat(ctx.Projects.SelectMany(p => p.Packages.Select(pk => new { from = p.Name, to = pk.Name, kind = pk.Name.Contains(".Kit.", StringComparison.Ordinal) ? "uses-kit" : "uses-package" })))
                .OrderBy(e => e.from, StringComparer.Ordinal).ThenBy(e => e.to, StringComparer.Ordinal).ToList();
            var layers = ctx.Projects.GroupBy(p => p.Layer.ToString()).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
            var text = $"graph: {nodes.Count} project(s), {edges.Count} edge(s), layout {ctx.Layout}\n" + string.Join("\n", layers.Select(l => $"  {l.Key}: {l.Value}"));
            return new OperationResult(true, text, new { layout = ctx.Layout, standards = ctx.Standards, layers, nodes, edges });
        });
}
