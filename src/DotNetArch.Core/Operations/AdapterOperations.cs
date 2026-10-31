using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Operations that make a module ready to be composed (D-40, Phase 15). <c>move_code</c> moves files from one project to another with the namespace,
/// every <c>using</c> and qualified reference in the repository, and the project and package references the moved code needs; the common case is code
/// that belongs to one adapter but sits in the shared Infrastructure project. <c>add_composition</c> creates the small project that references the
/// chosen adapters and selects one of them from a configuration key, so Infrastructure (and the module core) no longer reference any adapter.
/// Both name nothing that belongs to an organisation: names come from the project graph. Plan first; writes only with apply.
/// </summary>
internal static partial class AdapterOperations
{
    public static IReadOnlyList<OperationDefinition> All => new[] { MoveCode, AddComposition, SplitRegistration, AddAdapter };

    private static OperationParameter PathParam() => new("path", "Repository root (default: current folder).", Positional: true, Default: "@cwd");

    public static OperationDefinition MoveCode { get; } = new(
        "move_code",
        "Move files or folders from one project to another: the namespace declaration follows the target project, every using and qualified reference in the repository is rewritten (a using of a namespace that no longer exists is dropped), and the project and package references the moved code needs are added to the target. Typical use: adapter code that sits in the shared Infrastructure project moves into the adapter project. What cannot be decided mechanically is listed as manual; build afterwards to verify. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(),
            new OperationParameter("from", "Name of the source project (for example OrderService.Infrastructure).", Required: true),
            new OperationParameter("to", "Name of the target project (for example OrderService.Infrastructure.SepidarRest).", Required: true),
            new OperationParameter("files", "Comma-separated files or folders, relative to the source project folder.", Required: true),
            new OperationParameter("except", "Comma-separated files or folders to leave out of the moved folders."),
            new OperationParameter("namespace_from", "Namespace prefix that is replaced (default: the source project name)."),
            new OperationParameter("namespace_to", "Namespace prefix that replaces it (default: the target project name)."),
            new OperationParameter("into", "Folder in the target project that receives the files (default: the same relative path). Needed to move within one project."),
            new OperationParameter("make_public", "Declare the moved top-level types public (a type that moves to a project others consume cannot stay internal).", ParameterType.Flag),
        },
        RunMoveCode);

    public static OperationDefinition AddComposition { get; } = new(
        "add_composition",
        "Create the composition project of a module: <App>.Composition references the module's Infrastructure project and the named adapter projects and offers Add<Module>Composition(), which registers Infrastructure and then the adapter selected by a configuration key (the first adapter listed is the default). The call sites of Add<...>Infrastructure outside Infrastructure are rewritten to it and their projects reference the new one. The adapter registration method is found in each adapter's DependencyInjection file. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(),
            new OperationParameter("adapters", "Comma-separated adapter names (the part after <App>.Infrastructure.); the first is the default.", Required: true),
            new OperationParameter("module", "Module name for the method Add<Module>Composition (default: the application name without a Service suffix)."),
            new OperationParameter("selector_key", "Configuration key that selects the adapter (default: ServiceProvider:DefaultProvider).", Default: "ServiceProvider:DefaultProvider"),
            new OperationParameter("selector_env", "Environment variable read when the key is not configured (a historical fallback)."),
            new OperationParameter("aliases", "Comma-separated Adapter=Alias pairs: another value that selects an adapter (for example SepidarDB=DB)."),
            new OperationParameter("always", "Comma-separated adapter names that are registered whatever the selector says (an adapter that calls another module: it has no alternative until an in-process or event adapter exists)."),
            new OperationParameter("regenerate", "Rewrite DependencyInjection.cs of an existing composition project (the file is generated) and add the references to adapters it does not reference yet; call sites are left as they are.", ParameterType.Flag),
        },
        RunAddComposition);

    // ------------------------------------------------------------------------------------------------------------------------------

    private static string Root(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        return Directory.Exists(root) ? root : throw new ArgumentException($"Folder not found: {root}");
    }

    private static RepoContext Context(string root)
    {
        var profile = NetArchStore.ResolveProfile(root, null, out _);
        var standards = NetArchStore.LoadState(root)?.Standards ?? new List<string>();
        return new RepoContext(root, profile, new RuleSettings(), standards.Append(AbpChecks.Standard).Distinct());
    }

    private static ProjectInfo Project(RepoContext ctx, string? name, string parameter)
    {
        var value = name ?? throw new ArgumentException($"Missing required value '{parameter}'.");
        return ctx.Source.FirstOrDefault(p => p.Name.Equals(value, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"No project named '{value}' in the repository.");
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

    private static bool InNamespace(string name, string prefix) =>
        name.Equals(prefix, StringComparison.Ordinal) || name.StartsWith(prefix + ".", StringComparison.Ordinal);

    // ------------------------------------------------------------------------------------------------------------------------------
    // move_code

    private static OperationResult RunMoveCode(OperationRequest request)
    {
        var root = Root(request);
        var ctx = Context(root);
        var from = Project(ctx, request.Get("from"), "from");
        var to = Project(ctx, request.Get("to"), "to");
        var into = request.Get("into")?.Trim('/');
        if (from.Name.Equals(to.Name, StringComparison.OrdinalIgnoreCase) && into == null)
            throw new ArgumentException("'from' and 'to' are the same project: name the receiving folder with 'into'.");

        var nsFrom = request.Get("namespace_from") ?? from.Name;
        var nsTo = request.Get("namespace_to") ?? to.Name;
        var manual = new List<string>();

        var inside = (request.Get("files") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var except = (request.Get("except") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sourceFiles = new SortedSet<string>(StringComparer.Ordinal);
        var below = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in inside)
        {
            var path = $"{from.Dir}/{entry.Trim('/')}";
            var matches = ctx.Files.Where(f => f.Equals(path, StringComparison.Ordinal) || f.StartsWith(path + "/", StringComparison.Ordinal)).ToList();
            if (matches.Count == 0)
                throw new ArgumentException($"Nothing found in {from.Name}: {entry}");
            foreach (var match in matches)
            {
                sourceFiles.Add(match);
                below[match] = match.Equals(path, StringComparison.Ordinal) ? Path.GetFileName(match) : match[(path.Length + 1)..];
            }
        }

        sourceFiles.RemoveWhere(f => except.Any(e =>
        {
            var path = $"{from.Dir}/{e.Trim('/')}";
            return f.Equals(path, StringComparison.Ordinal) || f.StartsWith(path + "/", StringComparison.Ordinal);
        }));
        if (sourceFiles.Count == 0)
            throw new ArgumentException("Every file was excluded; nothing to move.");

        var moved = sourceFiles.Select(f => (Old: f, New: into == null ? $"{to.Dir}/{f[(from.Dir.Length + 1)..]}" : $"{to.Dir}/{into}/{below[f]}")).ToList();
        foreach (var item in moved)
        {
            if (ctx.Has(item.New))
                throw new ArgumentException($"The target already has {item.New}.");
        }

        // 1. namespaces: old -> new, and the types that move
        var namespaceMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var movedTypes = new Dictionary<string, List<(string OldNs, string NewNs)>>(StringComparer.Ordinal);
        var pairTokens = new Dictionary<(string OldNs, string NewNs), HashSet<string>>();
        var movedFromNamespace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var movedUsings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (oldPath, newPath) in moved)
        {
            var text = ctx.Read(oldPath, maxBytes: 4 * 1024 * 1024);
            if (!oldPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                contents[newPath] = text;
                continue;
            }

            var declared = NamespaceDeclaration().Match(text);
            if (!declared.Success)
            {
                manual.Add($"{oldPath} declares no namespace; moved as it is.");
                contents[newPath] = text;
                continue;
            }

            var oldNs = declared.Groups["ns"].Value;
            var newNs = InNamespace(oldNs, nsFrom) ? nsTo + oldNs[nsFrom.Length..] : oldNs;
            movedFromNamespace[newPath] = oldNs;
            if (newNs == oldNs && !oldNs.Equals(nsTo, StringComparison.Ordinal))
                manual.Add($"{oldPath}: namespace {oldNs} is outside {nsFrom}; left unchanged.");
            if (newNs != oldNs)
            {
                if (!namespaceMap.TryGetValue(oldNs, out var set))
                    namespaceMap[oldNs] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(newNs);
                if (!pairTokens.TryGetValue((oldNs, newNs), out var tokens))
                    pairTokens[(oldNs, newNs)] = tokens = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match type in TypeDeclaration().Matches(text))
                {
                    var typeName = type.Groups["name"].Value;
                    tokens.Add(typeName);
                    if (!movedTypes.TryGetValue(typeName, out var list))
                        movedTypes[typeName] = list = new List<(string, string)>();
                    list.Add((oldNs, newNs));
                }

                foreach (Match extension in ExtensionMethod().Matches(text))
                    tokens.Add(extension.Groups["name"].Value);
            }

            foreach (Match use in Using().Matches(text))
                movedUsings.Add(use.Groups["ns"].Value);
            var rewritten = NamespaceDeclaration().Replace(text, m => m.Value.Replace(oldNs, newNs, StringComparison.Ordinal), 1);
            if (request.Flag("make_public"))
                rewritten = InternalType().Replace(rewritten, "${lead}public ");
            contents[newPath] = rewritten;
        }

        // 2. every other .cs file: usings and qualified type names follow the move
        var movedOld = moved.ToDictionary(m => m.Old, m => m.New, StringComparer.OrdinalIgnoreCase);
        var finalFiles = ctx.Files.Where(f => !movedOld.ContainsKey(f)).Concat(moved.Select(m => m.New)).ToList();
        var declaredAfter = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var file in finalFiles.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var text = contents.TryGetValue(file, out var c) ? c : ctx.Read(file, maxBytes: 4 * 1024 * 1024);
            foreach (Match match in NamespaceDeclaration().Matches(text))
            {
                var ns = match.Groups["ns"].Value;
                if (!declaredAfter.TryGetValue(ns, out var tokens))
                    declaredAfter[ns] = tokens = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match type in TypeDeclaration().Matches(text))
                    tokens.Add(type.Groups["name"].Value);
                foreach (Match extension in ExtensionMethod().Matches(text))
                    tokens.Add(extension.Groups["name"].Value);
            }
        }

        var edited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in finalFiles.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var original = contents.TryGetValue(file, out var c) ? c : ctx.Read(file, maxBytes: 4 * 1024 * 1024);
            if (original.Length == 0)
                continue;
            var text = RewriteReferences(original, namespaceMap, movedTypes, pairTokens, declaredAfter, movedFromNamespace.TryGetValue(file, out var was) ? was : null, from.Name.Equals(to.Name, StringComparison.OrdinalIgnoreCase));
            if (text != original)
                edited[file] = text;
        }

        // 3. project and package references the moved code needs
        var targetFile = to.File;
        var targetText = ctx.Read(targetFile);
        var added = new List<string>();
        foreach (var reference in from.ProjectReferences)
        {
            var name = Path.GetFileNameWithoutExtension(reference);
            if (name.Equals(to.Name, StringComparison.OrdinalIgnoreCase) || to.ProjectReferences.Any(r => Path.GetFileNameWithoutExtension(r).Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!movedUsings.Any(u => InNamespace(u, name)))
                continue;
            var other = ctx.Projects.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (other == null || other.ProjectReferences.Any(r => Path.GetFileNameWithoutExtension(r).Equals(to.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            var include = Path.GetRelativePath(to.Dir, other.File).Replace('/', '\\');
            targetText = AddItem(targetText, "ProjectReference", include);
            added.Add($"ProjectReference {other.Name}");
        }

        // packages: the namespaces the moved code uses must resolve in the target. They are looked up in the compile assets of the restored projects
        // (the namespace of an assembly need not equal its package id); without restored assets the package id is compared with the namespace instead.
        // internal types that moved stay visible to the same friends (test projects): the source declares them in its project file or in an assembly attribute
        if (contents.Values.Any(c => InternalDeclaration().IsMatch(c)))
        {
            var friends = Regex.Matches(ctx.Read(from.File), "InternalsVisibleTo\\s+Include=\"(?<n>[^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => m.Groups["n"].Value)
                .Concat(ctx.Files.Where(f => f.StartsWith(from.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(f => Regex.Matches(ctx.Read(f), "\\[assembly:\\s*InternalsVisibleTo\\(\"(?<n>[^\"]+)\"\\)\\]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => m.Groups["n"].Value)))
                .Distinct(StringComparer.Ordinal);
            foreach (var friend in friends.Where(fr => !targetText.Contains($"InternalsVisibleTo Include=\"{fr}\"", StringComparison.Ordinal)))
            {
                targetText = AddItem(targetText, "InternalsVisibleTo", friend);
                added.Add($"InternalsVisibleTo {friend}");
            }
        }

        // the shared framework the source references and the moved code uses (ASP.NET Core types are not in a package)
        if (movedUsings.Any(u => InNamespace(u, "Microsoft.AspNetCore")) && ctx.Read(from.File).Contains("FrameworkReference Include=\"Microsoft.AspNetCore.App\"", StringComparison.Ordinal)
            && !targetText.Contains("FrameworkReference Include=\"Microsoft.AspNetCore.App\"", StringComparison.Ordinal))
        {
            targetText = AddItem(targetText, "FrameworkReference", "Microsoft.AspNetCore.App");
            added.Add("FrameworkReference Microsoft.AspNetCore.App");
        }

        var propsText = ctx.Read("Directory.Packages.props");
        var propsEdited = RequirePackages(ctx, root, from, to, movedUsings, ref targetText, ref propsText, added);

        var plan = new List<FixAction>();
        foreach (var (oldPath, newPath) in moved)
        {
            plan.Add(new FixAction(new PlannedChange(newPath, "move", $"moved from {from.Name}", "move_code"), string.Empty, MoveFrom: oldPath));
            var body = edited.TryGetValue(newPath, out var rewritten) ? rewritten : contents[newPath];
            plan.Add(new FixAction(new PlannedChange(newPath, "modify", "namespace and references follow the move", "move_code"), body));
        }

        foreach (var (file, text) in edited.Where(e => !movedOld.Values.Contains(e.Key, StringComparer.OrdinalIgnoreCase)))
            plan.Add(new FixAction(new PlannedChange(file, "modify", "usings and qualified names follow the move", "move_code"), text));
        // a project that referenced the source and names the moved code (a test project, a host) now needs the target too
        bool Is(string reference, string name) => Path.GetFileNameWithoutExtension(reference).Equals(name, StringComparison.OrdinalIgnoreCase);
        foreach (var consumer in ctx.Projects.Where(p => !p.Name.Equals(from.Name, StringComparison.OrdinalIgnoreCase) && !p.Name.Equals(to.Name, StringComparison.OrdinalIgnoreCase)
                                                        && p.ProjectReferences.Any(r => Is(r, from.Name)) && !p.ProjectReferences.Any(r => Is(r, to.Name))
                                                        && !to.ProjectReferences.Any(r => Is(r, p.Name))
                                                        && edited.Keys.Any(f => f.StartsWith(p.Dir + "/", StringComparison.Ordinal))))
        {
            var include = Path.GetRelativePath(consumer.Dir, to.File).Replace('/', '\\');
            plan.Add(new FixAction(new PlannedChange(consumer.File, "modify", $"ProjectReference {to.Name}", "move_code"), AddItem(ctx.Read(consumer.File), "ProjectReference", include)));
        }

        if (added.Count > 0)
            plan.Add(new FixAction(new PlannedChange(targetFile, "modify", string.Join(", ", added), "move_code"), targetText));
        if (propsEdited)
            plan.Add(new FixAction(new PlannedChange("Directory.Packages.props", "modify", "central versions for the packages the target now uses", "move_code"), propsText));

        manual.Add($"build the solution to verify; if {from.Name} no longer uses a package it has, remove it (doctor DA-A11 reports unused references).");
        return Planned("move_code", root, plan, request.Apply, manual);
    }

    private static string RewriteReferences(
        string text,
        Dictionary<string, HashSet<string>> namespaceMap,
        Dictionary<string, List<(string OldNs, string NewNs)>> movedTypes,
        Dictionary<(string OldNs, string NewNs), HashSet<string>> pairTokens,
        Dictionary<string, HashSet<string>> declaredAfter,
        string? movedFromNs,
        bool sameProject)
    {
        var words = new HashSet<string>(Word().Matches(text).Select(m => m.Value), StringComparer.Ordinal);
        var existing = new HashSet<string>(text.Split('\n').Select(l => l.Trim()), StringComparer.Ordinal);
        var output = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var match = UsingLine().Match(line);
            if (!match.Success || !namespaceMap.TryGetValue(match.Groups["ns"].Value, out var replacements))
            {
                output.Add(line);
                continue;
            }

            // the old using stays only while the file still uses something that is left in that namespace
            var ns = match.Groups["ns"].Value;
            var carriage = line.EndsWith('\r') ? "\r" : string.Empty;
            if (declaredAfter.TryGetValue(ns, out var remaining) && remaining.Overlaps(words))
                output.Add(line);
            else
                existing.Remove(line.Trim());
            foreach (var replacement in replacements.OrderBy(r => r, StringComparer.Ordinal))
            {
                if (!pairTokens.TryGetValue((ns, replacement), out var tokens) || !tokens.Overlaps(words))
                    continue;
                var produced = $"using {match.Groups["static"].Value}{replacement};";
                if (existing.Add(produced))
                    output.Add(match.Groups["indent"].Value + produced + carriage);
            }
        }

        // a file that sat in the old namespace (or below it) needed no using for its siblings; after the move it does
        var fileNamespace = NamespaceDeclaration().Match(text) is { Success: true } fn ? fn.Groups["ns"].Value : null;
        var needed = new List<string>();
        foreach (var ((oldNs, newNs), tokens) in pairTokens)
        {
            var inOld = (fileNamespace != null && InNamespace(fileNamespace, oldNs)) || (movedFromNs != null && InNamespace(movedFromNs, oldNs));
            if (inOld && (fileNamespace == null || !InNamespace(fileNamespace, newNs)) && tokens.Overlaps(words) && !existing.Contains($"using {newNs};"))
                needed.Add(newNs);
        }

        if (movedFromNs != null && sameProject)
        {
            foreach (var (declaredNs, tokens) in declaredAfter)
            {
                if (declaredNs != fileNamespace && InNamespace(movedFromNs, declaredNs) && tokens.Overlaps(words) && !existing.Contains($"using {declaredNs};"))
                    needed.Add(declaredNs);
            }
        }

        if (needed.Count > 0)
        {
            var lastUsing = output.FindLastIndex(l => UsingLine().IsMatch(l));
            var insertion = needed.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).Select(n => $"using {n};").ToList();
            if (lastUsing >= 0)
                output.InsertRange(lastUsing + 1, insertion);
            else
                output.InsertRange(0, insertion.Append(string.Empty));
        }

        var result = string.Join('\n', output);
        foreach (var (type, places) in movedTypes)
        {
            foreach (var (oldNs, newNs) in places.Distinct())
            {
                // the full name first, then the trailing parts of the namespace (a name relative to an enclosing namespace, for example Common.Configuration.X)
                var segments = oldNs.Split('.');
                for (var take = segments.Length; take >= 1; take--)
                {
                    result = Regex.Replace(
                        result,
                        @"(?<![\w.])" + Regex.Escape(string.Join('.', segments[^take..])) + @"\." + Regex.Escape(type) + @"\b",
                        newNs + "." + type,
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromSeconds(2));
                }
            }
        }

        return result;
    }

    /// <summary>Adds an item to the first item group that already holds the same kind, or to a new one; the text keeps its layout.</summary>
    internal static string AddItem(string text, string kind, string include, string attributes = "")
    {
        if (text.Contains($"{kind} Include=\"{include}\"", StringComparison.OrdinalIgnoreCase))
            return text;
        var line = $"    <{kind} Include=\"{include}\"{attributes} />\n";
        var index = text.LastIndexOf($"<{kind} ", StringComparison.Ordinal);
        if (index >= 0)
        {
            var end = text.IndexOf('\n', index);
            if (end >= 0)
                return text[..(end + 1)] + line + text[(end + 1)..];
        }

        var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        return close < 0 ? text : text[..close].TrimEnd('\n') + "\n\n  <ItemGroup>\n" + line + "  </ItemGroup>\n" + text[close..];
    }

    // ------------------------------------------------------------------------------------------------------------------------------
    // add_composition

    private static OperationResult RunAddComposition(OperationRequest request)
    {
        var root = Root(request);
        var ctx = Context(root);
        var infrastructure = ctx.OfLayer(ProjectLayer.Infrastructure).FirstOrDefault(p => p.Name.EndsWith(".Infrastructure", StringComparison.Ordinal))
                             ?? throw new ArgumentException("No <App>.Infrastructure project found.");
        var app = infrastructure.Name[..^".Infrastructure".Length];
        var module = request.Get("module") ?? (app.EndsWith("Service", StringComparison.Ordinal) ? app[..^"Service".Length] : app);
        var selector = request.Get("selector_key") ?? "ServiceProvider:DefaultProvider";
        var names = (request.Get("adapters") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
            throw new ArgumentException("Name at least one adapter.");
        var compositionName = $"{app}.Composition";
        var existing = ctx.Projects.FirstOrDefault(p => p.Name.Equals(compositionName, StringComparison.OrdinalIgnoreCase));
        var regenerate = request.Flag("regenerate");
        if (existing != null && !regenerate)
            throw new ArgumentException($"{compositionName} already exists (use regenerate to rewrite its generated DependencyInjection.cs).");
        var aliases = (request.Get("aliases") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.Split('=', 2)).Where(a => a.Length == 2).ToLookup(a => a[0].Trim(), a => a[1].Trim(), StringComparer.OrdinalIgnoreCase);
        var selectorEnv = request.Get("selector_env");

        var infraDi = ctx.Files.FirstOrDefault(f => f.StartsWith(infrastructure.Dir + "/", StringComparison.Ordinal) && f.EndsWith("DependencyInjection.cs", StringComparison.Ordinal))
                      ?? throw new ArgumentException($"{infrastructure.Name} has no DependencyInjection.cs.");
        var infraText = ctx.Read(infraDi);
        var infraMethod = RegistrationMethod().Matches(infraText).Cast<Match>().FirstOrDefault(m => m.Groups["name"].Value.EndsWith("Infrastructure", StringComparison.Ordinal))
                          ?? throw new ArgumentException($"{infraDi} has no public Add...Infrastructure extension method.");
        var infraNs = NamespaceDeclaration().Match(infraText).Groups["ns"].Value;
        var infraParameters = Regex.Replace(infraMethod.Groups["params"].Value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        if (!infraParameters.Contains("IServiceCollection services", StringComparison.Ordinal) || !infraParameters.Contains("IConfiguration configuration", StringComparison.Ordinal))
            throw new ArgumentException($"{infraMethod.Groups["name"].Value} must take 'IServiceCollection services' and 'IConfiguration configuration' (the generated method forwards them).");

        var always = (request.Get("always") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var adapters = new List<(string Name, ProjectInfo Project, string Namespace, string Method, bool Workers, string? WorkersName)>();
        var alwaysAdapters = new List<(string Name, ProjectInfo Project, string Namespace, string Method, bool Workers, string? WorkersName)>();
        foreach (var (list, group) in new[] { (names, adapters), (always, alwaysAdapters) })
        {
            foreach (var name in list)
            {
                var project = ctx.Source.FirstOrDefault(p => p.Name.Equals($"{app}.Infrastructure.{name}", StringComparison.OrdinalIgnoreCase))
                              ?? throw new ArgumentException($"No adapter project {app}.Infrastructure.{name}.");
                var di = ctx.Files.FirstOrDefault(f => f.StartsWith(project.Dir + "/", StringComparison.Ordinal) && f.EndsWith("DependencyInjection.cs", StringComparison.Ordinal))
                         ?? throw new ArgumentException($"{project.Name} has no DependencyInjection.cs.");
                var text = ctx.Read(di);
                var method = RegistrationMethod().Matches(text).Cast<Match>().FirstOrDefault()
                             ?? throw new ArgumentException($"{di} has no public registration extension method.");
                var workers = WorkersParameter().Match(method.Groups["params"].Value);
                group.Add((name, project, NamespaceDeclaration().Match(text).Groups["ns"].Value, method.Groups["name"].Value, workers.Success, workers.Success ? workers.Groups["name"].Value : null));
            }
        }

        // one background-workers flag for the whole composition: the name the Infrastructure method uses, else the first adapter's
        var infraWorkers = WorkersParameter().Match(infraParameters) is { Success: true } iw ? iw.Groups["name"].Value : null;
        var workersName = infraWorkers ?? adapters.Concat(alwaysAdapters).Where(a => a.Workers).Select(a => a.WorkersName).FirstOrDefault();
        var usings = new SortedSet<string>(StringComparer.Ordinal) { "Microsoft.Extensions.Configuration", "Microsoft.Extensions.DependencyInjection", infraNs };
        foreach (var adapter in adapters.Concat(alwaysAdapters))
            usings.Add(adapter.Namespace);
        var code = new StringBuilder();
        foreach (var u in usings)
            code.Append($"using {u};\n");
        code.Append($"\nnamespace {compositionName};\n\n");
        code.Append("/// <summary>\n");
        code.Append($"/// Composition root of the {module} module: registers the module's Infrastructure and the adapter selected by the configuration key <see cref=\"AdapterKey\"/>.\n");
        code.Append($"/// A missing or unknown value selects <c>{adapters[0].Name}</c>. Generated by dotnet-arch add_composition; it stands in for the host a composition generates later.\n");
        code.Append("/// </summary>\n");
        code.Append("public static class DependencyInjection\n{\n");
        code.Append($"    /// <summary>Configuration key that selects the adapter.</summary>\n    public const string AdapterKey = \"{selector}\";\n\n");
        var signature = "this IServiceCollection services, IConfiguration configuration" + (workersName == null ? string.Empty : $", bool {workersName} = true");
        code.Append($"    public static IServiceCollection Add{module}Composition({signature})\n    {{\n");
        var infraCall = $"services.{infraMethod.Groups["name"].Value}(configuration{(infraWorkers == null ? string.Empty : $", {infraWorkers}")});";
        code.Append($"        {infraCall}\n");
        foreach (var a in alwaysAdapters)
            code.Append($"        services.{a.Method}(configuration{(a.Workers && workersName != null ? $", {workersName}" : string.Empty)});\n");
        code.Append(selectorEnv == null
            ? "        var selected = configuration[AdapterKey];\n"
            : $"        var selected = configuration[AdapterKey] ?? Environment.GetEnvironmentVariable(\"{selectorEnv}\");\n");
        for (var i = 1; i < adapters.Count; i++)
        {
            var a = adapters[i];
            var accepted = new[] { a.Name }.Concat(aliases[a.Name]).Distinct(StringComparer.OrdinalIgnoreCase);
            code.Append($"        if ({string.Join(" || ", accepted.Select(n => $"string.Equals(selected, \"{n}\", StringComparison.OrdinalIgnoreCase)"))})\n");
            code.Append($"            return services.{a.Method}(configuration{(a.Workers && workersName != null ? $", {workersName}" : string.Empty)});\n");
        }

        var d = adapters[0];
        code.Append($"        return services.{d.Method}(configuration{(d.Workers && workersName != null ? $", {workersName}" : string.Empty)});\n");
        code.Append("    }\n}\n");

        var framework = XDocument.Load(Path.Combine(root, infrastructure.File)).Descendants("TargetFramework").FirstOrDefault()?.Value ?? "net9.0";
        var dir = $"{infrastructure.Dir[..infrastructure.Dir.LastIndexOf('/')]}/{compositionName}";
        var csprojPath = $"{dir}/{compositionName}.csproj";
        var references = new StringBuilder();
        foreach (var target in new[] { infrastructure }.Concat(adapters.Concat(alwaysAdapters).Select(a => a.Project)))
            references.Append($"    <ProjectReference Include=\"{Path.GetRelativePath(dir, target.File).Replace('/', '\\')}\" />\n");
        var csproj = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>{framework}</TargetFramework>\n    <ImplicitUsings>enable</ImplicitUsings>\n    <Nullable>enable</Nullable>\n  </PropertyGroup>\n\n  <ItemGroup>\n{references}  </ItemGroup>\n\n</Project>\n";
        var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        var plan = new List<FixAction>();
        if (existing == null)
            plan.Add(new(new PlannedChange(csprojPath, "create", $"composition project of {module}", "add_composition"), csproj, solution == null ? null : () => RegisterInSolution(root, solution, csprojPath)));
        plan.Add(new(new PlannedChange($"{existing?.Dir ?? dir}/DependencyInjection.cs", existing == null ? "create" : "modify", $"Add{module}Composition", "add_composition"), code.ToString()));
        if (existing != null)
        {
            var existingText = ctx.Read(existing.File);
            var added = new List<string>();
            foreach (var target in adapters.Concat(alwaysAdapters).Select(a => a.Project).Where(p => !existing.ProjectReferences.Any(r => Path.GetFileNameWithoutExtension(r).Equals(p.Name, StringComparison.OrdinalIgnoreCase))))
            {
                existingText = AddItem(existingText, "ProjectReference", Path.GetRelativePath(existing.Dir, target.File).Replace('/', '\\'));
                added.Add(target.Name);
            }

            if (added.Count > 0)
                plan.Add(new FixAction(new PlannedChange(existing.File, "modify", $"references {string.Join(", ", added)}", "add_composition"), existingText));
            return Planned("add_composition", root, plan, request.Apply, new[] { "regenerated DependencyInjection.cs and the adapter references; run fix --rules=DA-A11 for the Dockerfile restore lines" });
        }

        // call sites outside Infrastructure and the new project now use the composition method
        var callPattern = new Regex(@"(?<=\.)" + Regex.Escape(infraMethod.Groups["name"].Value) + @"(?=\s*\()", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        var projectEdits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in ctx.Files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !f.StartsWith(infrastructure.Dir + "/", StringComparison.Ordinal)))
        {
            var text = ctx.Read(file);
            if (!callPattern.IsMatch(text))
                continue;
            var rewritten = callPattern.Replace(text, $"Add{module}Composition");
            var usingLine = $"using {compositionName};";
            if (!rewritten.Contains(usingLine, StringComparison.Ordinal))
            {
                var last = Regex.Matches(rewritten, @"(?m)^using [^\r\n]+;\r?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                var insertAt = last.Count > 0 ? last[^1].Index + last[^1].Length : 0;
                var newline = rewritten.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                rewritten = rewritten.Insert(insertAt, (insertAt == 0 ? string.Empty : newline) + usingLine + (insertAt == 0 ? newline : string.Empty));
            }

            plan.Add(new FixAction(new PlannedChange(file, "modify", $"calls Add{module}Composition", "add_composition"), rewritten));
            var owner = ctx.Projects.Where(p => file.StartsWith(p.Dir + "/", StringComparison.Ordinal)).OrderByDescending(p => p.Dir.Length).FirstOrDefault();
            if (owner != null && !owner.Name.Equals(compositionName, StringComparison.OrdinalIgnoreCase) && !projectEdits.ContainsKey(owner.File))
            {
                var include = Path.GetRelativePath(owner.Dir, csprojPath).Replace('/', '\\');
                projectEdits[owner.File] = AddItem(ctx.Read(owner.File), "ProjectReference", include);
            }
        }

        foreach (var (file, text) in projectEdits)
            plan.Add(new FixAction(new PlannedChange(file, "modify", $"references {compositionName}", "add_composition"), text));
        var manual = new List<string>
        {
            "run fix --rules=DA-A11 to complete the Dockerfile restore lines for the new project",
            $"{infrastructure.Name} must no longer reference the adapter projects: move adapter-specific code out with move_code, then remove the references",
        };
        return Planned("add_composition", root, plan, request.Apply, manual);
    }

    /// <summary>
    /// Namespace -> package (id, version) for the compile assemblies of a restored project, read from <c>obj/project.assets.json</c> and the assembly metadata
    /// in the package folder. Empty when the project is not restored; the caller then falls back to comparing package ids with namespaces.
    /// </summary>
    internal static Dictionary<string, (string Id, string Version)> NamespaceIndex(string projectDirectory)
    {
        var index = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        var assets = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assets))
            return index;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(assets));
            var root = document.RootElement;
            var folders = root.TryGetProperty("packageFolders", out var f) ? f.EnumerateObject().Select(o => o.Name).ToList() : new List<string>();
            if (!root.TryGetProperty("targets", out var targets))
                return index;
            foreach (var target in targets.EnumerateObject().Take(1))
            {
                foreach (var library in target.Value.EnumerateObject())
                {
                    var slash = library.Name.IndexOf('/', StringComparison.Ordinal);
                    if (slash < 0 || !library.Value.TryGetProperty("compile", out var compile))
                        continue;
                    var id = library.Name[..slash];
                    var version = library.Name[(slash + 1)..];
                    foreach (var dll in compile.EnumerateObject().Select(o => o.Name).Where(n => n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        foreach (var folder in folders)
                        {
                            var path = Path.Combine(folder, id.ToLowerInvariant(), version, dll.Replace('/', Path.DirectorySeparatorChar));
                            if (!File.Exists(path))
                                continue;
                            foreach (var ns in Namespaces(path))
                                index.TryAdd(ns, (id, version));
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or BadImageFormatException or InvalidOperationException)
        {
            return index;
        }

        return index;
    }

    /// <summary>
    /// The public type names and public static method names (extension methods among them) of each namespace the restored compile assets of the project offer. Empty when the project is not
    /// restored; a caller then keeps what it cannot decide.
    /// </summary>
    internal static Dictionary<string, HashSet<string>> NamespaceMembers(string projectDirectory)
    {
        var members = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var assets = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assets))
            return members;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(assets));
            var root = document.RootElement;
            var folders = root.TryGetProperty("packageFolders", out var f) ? f.EnumerateObject().Select(o => o.Name).ToList() : new List<string>();
            if (!root.TryGetProperty("targets", out var targets))
                return members;
            foreach (var target in targets.EnumerateObject().Take(1))
            {
                foreach (var library in target.Value.EnumerateObject())
                {
                    var slash = library.Name.IndexOf('/', StringComparison.Ordinal);
                    if (slash < 0 || !library.Value.TryGetProperty("compile", out var compile))
                        continue;
                    var id = library.Name[..slash];
                    var version = library.Name[(slash + 1)..];
                    foreach (var dll in compile.EnumerateObject().Select(o => o.Name).Where(n => n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        foreach (var folder in folders)
                        {
                            var path = Path.Combine(folder, id.ToLowerInvariant(), version, dll.Replace('/', Path.DirectorySeparatorChar));
                            if (!File.Exists(path))
                                continue;
                            AddMembers(path, members);
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or BadImageFormatException or InvalidOperationException)
        {
            return members;
        }

        return members;
    }

    private static void AddMembers(string assembly, Dictionary<string, HashSet<string>> members)
    {
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
            return;
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            var ns = reader.GetString(type.Namespace);
            if (ns.Length == 0 || (type.Attributes & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public)
                continue;
            if (!members.TryGetValue(ns, out var names))
                members[ns] = names = new HashSet<string>(StringComparer.Ordinal);
            var name = reader.GetString(type.Name);
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            names.Add(tick < 0 ? name : name[..tick]);
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if ((method.Attributes & System.Reflection.MethodAttributes.Static) != 0 && (method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) == System.Reflection.MethodAttributes.Public)
                    names.Add(reader.GetString(method.Name));
            }
        }
    }

    /// <summary>The type and extension-method names the source projects of the repository declare, by namespace.</summary>
    internal static Dictionary<string, HashSet<string>> SourceMembers(RepoContext ctx)
    {
        var members = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var project in ctx.Source)
        {
            foreach (var file in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var text = ctx.Read(file);
                var ns = NamespaceDeclaration().Match(text).Groups["ns"].Value;
                if (ns.Length == 0)
                    continue;
                if (!members.TryGetValue(ns, out var names))
                    members[ns] = names = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match type in TypeDeclaration().Matches(text))
                    names.Add(type.Groups["name"].Value);
                foreach (Match extension in ExtensionMethod().Matches(text))
                    names.Add(extension.Groups["name"].Value);
            }
        }

        return members;
    }

    private static IEnumerable<string> Namespaces(string assembly)
    {
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
            return Array.Empty<string>();
        var reader = pe.GetMetadataReader();
        return reader.TypeDefinitions
            .Select(h => reader.GetString(reader.GetTypeDefinition(h).Namespace))
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Adds the package references <paramref name="to"/> needs for the namespaces in <paramref name="usings"/> (code that moved from <paramref name="from"/>), with the central version when the
    /// repository manages versions centrally. Returns true when the props text changed.
    /// </summary>
    private static bool RequirePackages(RepoContext ctx, string root, ProjectInfo from, ProjectInfo to, IEnumerable<string> usings, ref string targetText, ref string propsText, List<string> added)
    {
        var propsEdited = false;
        var centrally = propsText.Contains("ManagePackageVersionsCentrally", StringComparison.OrdinalIgnoreCase) && propsText.Contains(">true<", StringComparison.OrdinalIgnoreCase);
        var fromIndex = NamespaceIndex(Path.Combine(root, from.Dir));
        var toIndex = NamespaceIndex(Path.Combine(root, to.Dir));
        foreach (var ns in usings.OrderBy(u => u, StringComparer.Ordinal))
        {
            if (ctx.Projects.Any(p => InNamespace(ns, p.Name)) || InNamespace(ns, "System"))
                continue;
            if (toIndex.ContainsKey(ns) || to.Packages.Any(p => InNamespace(ns, p.Name)))
                continue;
            (string Id, string Version)? provider = null;
            if (fromIndex.TryGetValue(ns, out var found))
                provider = found;
            else if (from.Packages.FirstOrDefault(p => InNamespace(ns, p.Name)) is { } direct)
                provider = (direct.Name, string.Empty);
            if (provider is null)
                continue;
            if (to.Packages.Any(p => p.Name.Equals(provider.Value.Id, StringComparison.OrdinalIgnoreCase)) || targetText.Contains($"PackageReference Include=\"{provider.Value.Id}\"", StringComparison.OrdinalIgnoreCase))
                continue;
            // a central version below the one the source resolves can only be reached there through another package, so it would be a downgrade here (NU1605): the target gets it the same way
            if (centrally && provider.Value.Version.Length > 0
                && Regex.Match(propsText, $"PackageVersion\\s+Include=\"{Regex.Escape(provider.Value.Id)}\"\\s+Version=\"(?<v>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is { Success: true } pinned
                && Version.TryParse(pinned.Groups["v"].Value, out var pinnedVersion) && Version.TryParse(provider.Value.Version, out var resolvedVersion) && pinnedVersion < resolvedVersion)
                continue;
            targetText = AddItem(targetText, "PackageReference", provider.Value.Id);
            added.Add($"PackageReference {provider.Value.Id}");
            if (centrally && provider.Value.Version.Length > 0 && !propsText.Contains($"Include=\"{provider.Value.Id}\"", StringComparison.OrdinalIgnoreCase))
            {
                propsText = AddItem(propsText, "PackageVersion", provider.Value.Id, $" Version=\"{provider.Value.Version}\"");
                propsEdited = true;
            }
        }
        return propsEdited;
    }

    private static void RegisterInSolution(string root, string solution, string project)
    {
        var result = ToolHost.Runner.Run(new ProcessSpec("dotnet", new[] { "sln", solution, "add", project }, root), showProgress: false);
        if (!result.Success)
            throw new InvalidOperationException($"dotnet sln add failed: {result.Output}");
    }

    [GeneratedRegex(@"(?m)^\s*namespace\s+(?<ns>[A-Za-z_][\w.]*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NamespaceDeclaration();

    [GeneratedRegex(@"(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?(?<ns>[A-Za-z_][\w.]*)\s*;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Using();

    [GeneratedRegex(@"^(?<indent>\s*)using\s+(?<static>static\s+)?(?<ns>[A-Za-z_][\w.]*)\s*;\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UsingLine();

    [GeneratedRegex(@"\b(?:class|record\s+struct|record\s+class|record|struct|interface|enum)\s+(?<name>[A-Za-z_]\w*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TypeDeclaration();

    [GeneratedRegex(@"(?m)^(?<lead>[ \t]*)internal\s+(?=(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|record|struct|interface|enum)\b)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex InternalType();

    [GeneratedRegex(@"\bstatic\s+[\w<>\[\],.? ]+?\s+(?<name>\w+)\s*(?:<[^>]*>)?\s*\(\s*this\s", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ExtensionMethod();

    [GeneratedRegex(@"(?m)^\s*(?:\[[^\]]*\]\s*)*internal\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex InternalDeclaration();

    [GeneratedRegex(@"\b[A-Za-z_]\w*\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Word();

    [GeneratedRegex(@"public\s+static\s+IServiceCollection\s+(?<name>Add\w+)\s*\(\s*(?<params>this\s+IServiceCollection\s+\w+[^)]*)\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RegistrationMethod();

    [GeneratedRegex(@"bool\s+(?<name>\w+)\s*(?:=\s*(?:true|false))?", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex WorkersParameter();
}
