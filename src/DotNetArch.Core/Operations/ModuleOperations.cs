using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// What a module offers to a composition, read from the code and never typed twice (Phase 15, D-40). <c>describe_module</c> writes <c>module.yaml</c>
/// (ports, adapters with the ports they implement and the settings they read, layers, composition project); <c>describe_settings</c> writes a
/// <c>settings.schema.json</c> (JSON Schema 2020-12, <c>additionalProperties: false</c>) next to each project that declares options classes, with secret-looking
/// properties marked <c>writeOnly</c>. Both are derived: running them again after a code change is the only way the documents change, so they cannot drift.
/// The tool names no organisation; everything comes from the project graph. Plan first; writes only with apply.
/// </summary>
internal static partial class ModuleOperations
{
    public static IReadOnlyList<OperationDefinition> All => new[] { DescribeModule, DescribeSettings };

    private static readonly string[] Secrets = { "password", "secret", "token", "apikey", "api_key", "connectionstring", "privatekey" };

    public static OperationDefinition DescribeModule { get; } = new(
        "describe_module",
        "Write module.yaml from the code: the ports (interfaces under Ports folders), the adapters (projects <App>.Infrastructure.<System>) with their package, registration method, the ports they implement and the configuration keys they read, the layers (HttpApi, Mcp, Cli, HttpApi.Client, Contracts) and the composition project with its selector key. Derived only; run it again after a change. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[] { new OperationParameter("path", "Repository root (default: current folder).", Positional: true, Default: "@cwd") },
        RunDescribeModule);

    public static OperationDefinition DescribeSettings { get; } = new(
        "describe_settings",
        "Write settings.schema.json next to every project that declares options classes (a class named ...Options with settable properties and a section name: a SectionName constant or the GetSection bound to it). JSON Schema 2020-12 with additionalProperties false; a property that looks like a secret is marked writeOnly. Classes whose section cannot be found are listed as manual. Derived only. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[] { new OperationParameter("path", "Repository root (default: current folder).", Positional: true, Default: "@cwd") },
        RunDescribeSettings);

    // ------------------------------------------------------------------------------------------------------------------------------

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

    private static string Quote(string value) => value.Length > 0 && Regex.IsMatch(value, @"^[A-Za-z0-9_./:@-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) && !value.EndsWith(':') ? value : "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string SourceOf(RepoContext ctx, ProjectInfo project) =>
        string.Join('\n', ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Select(f => ctx.Read(f, maxBytes: 4 * 1024 * 1024)));

    // ------------------------------------------------------------------------------------------------------------------------------
    // describe_module

    private static OperationResult RunDescribeModule(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        var ctx = Context(root);
        var host = AdapterChecks.Host(ctx) ?? throw new ArgumentException("No <App>.Infrastructure project found.");
        var app = host.Name[..^".Infrastructure".Length];
        var module = app.EndsWith("Service", StringComparison.Ordinal) ? app[..^"Service".Length] : app;

        var ports = new List<(string Name, string Namespace, string Project)>();
        foreach (var project in ctx.OfLayer(ProjectLayer.Domain))
        {
            foreach (var file in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.Ordinal) && f.Contains("/Ports/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var text = ctx.Read(file);
                var ns = NamespaceOf(text);
                foreach (Match match in PortInterface().Matches(text))
                    ports.Add((match.Groups["name"].Value, ns, project.Name));
            }
        }

        var portNames = ports.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var yaml = new StringBuilder();
        yaml.Append("# Derived by dotnet-arch describe_module. Do not edit: run it again after a code change.\n");
        yaml.Append("apiVersion: module/v1\n");
        yaml.Append($"module: {Quote(module)}\n");
        yaml.Append($"application: {Quote(app)}\n");
        // one version per repository: the version of the host project (the Api), as the release tags follow it
        var apiProject = ctx.OfLayer(ProjectLayer.Api).FirstOrDefault();
        var version = apiProject != null && VersionElement().Match(ctx.Read(apiProject.File)) is { Success: true } vm ? vm.Groups["v"].Value : null;
        yaml.Append($"version: {(version == null ? "null" : Quote(version))}\n");

        yaml.Append("layers:\n");
        foreach (var (name, layer) in new[] { ("api", ProjectLayer.HttpApi), ("client", ProjectLayer.HttpApiClient), ("mcp", ProjectLayer.Mcp), ("cli", ProjectLayer.Cli) })
        {
            var project = ctx.OfLayer(layer).FirstOrDefault();
            yaml.Append(project == null ? $"  {name}: null\n" : $"  {name}: {Quote(project.Name)}\n");
        }

        var contracts = ctx.OfLayer(ProjectLayer.Contracts).FirstOrDefault();
        yaml.Append($"contracts: {(contracts == null ? "null" : Quote(contracts.Name))}\n");

        yaml.Append(ports.Count == 0 ? "ports: []\n" : "ports:\n");
        foreach (var port in ports.OrderBy(p => p.Name, StringComparer.Ordinal))
            yaml.Append($"  - name: {Quote(port.Name)}\n    namespace: {Quote(port.Namespace)}\n    project: {Quote(port.Project)}\n");

        var adapters = AdapterChecks.Adapters(ctx).OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
        yaml.Append(adapters.Count == 0 ? "adapters: []\n" : "adapters:\n");
        foreach (var adapter in adapters)
        {
            var source = SourceOf(ctx, adapter);
            var csproj = ctx.Read(adapter.File);
            var package = PackageIdElement().Match(csproj) is { Success: true } pid ? pid.Groups["id"].Value : null;
            var registration = Registrations().Matches(source).Cast<Match>().Where(m => !m.Groups["lead"].Value.Contains("Obsolete", StringComparison.Ordinal)).Select(m => m.Groups["name"].Value).Distinct(StringComparer.Ordinal).ToList();
            var implemented = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match type in ClassBases().Matches(source))
            {
                foreach (var part in type.Groups["bases"].Value.Split(',', StringSplitOptions.TrimEntries))
                {
                    var bare = part.Contains('<', StringComparison.Ordinal) ? part[..part.IndexOf('<', StringComparison.Ordinal)] : part;
                    if (portNames.Contains(bare))
                        implemented.Add(bare);
                }
            }

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in ConfigurationKey().Matches(source))
                keys.Add(m.Groups["key"].Value);

            yaml.Append($"  - system: {Quote(AdapterChecks.SystemOf(ctx, adapter)!)}\n    project: {Quote(adapter.Name)}\n");
            yaml.Append($"    package: {(package == null ? "null" : Quote(package))}\n");
            var adapterVersion = VersionElement().Match(csproj) is { Success: true } av ? av.Groups["v"].Value : "1.0.0";
            yaml.Append($"    version: {Quote(adapterVersion)}\n");
            yaml.Append(registration.Count == 0 ? "    registration: []\n" : "    registration:\n" + string.Concat(registration.Select(r => $"      - {Quote(r)}\n")));
            yaml.Append(implemented.Count == 0 ? "    implements: []\n" : "    implements:\n" + string.Concat(implemented.Select(i => $"      - {Quote(i)}\n")));
            yaml.Append(keys.Count == 0 ? "    reads: []\n" : "    reads:\n" + string.Concat(keys.Select(k => $"      - {Quote(k)}\n")));
            yaml.Append($"    backgroundWorkers: {(Regex.IsMatch(source, @"registerBackgroundWorkers", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)) ? "true" : "false")}\n");
            var schema = $"{adapter.Dir}/settings.schema.json";
            yaml.Append($"    settingsSchema: {(ctx.Has(schema) ? Quote("settings.schema.json") : "null")}\n");
        }

        var composition = ctx.Source.FirstOrDefault(p => p.Name.EndsWith(".Composition", StringComparison.Ordinal));
        if (composition == null)
        {
            yaml.Append("composition: null\n");
        }
        else
        {
            var source = SourceOf(ctx, composition);
            var key = SelectorKey().Match(source) is { Success: true } k ? k.Groups["key"].Value : null;
            yaml.Append($"composition:\n  project: {Quote(composition.Name)}\n  selector: {(key == null ? "null" : Quote(key))}\n");
        }

        var manual = new List<string>();
        if (adapters.Any(a => !ctx.Read(a.File).Contains("<PackageId>", StringComparison.Ordinal)))
            manual.Add("an adapter has no PackageId (fix --rules=DA-AD05)");
        var plan = new List<FixAction> { new(new PlannedChange("module.yaml", ctx.Has("module.yaml") ? "modify" : "create", "derived from the code", "describe_module"), yaml.ToString()) };
        return Planned("describe_module", root, plan, request.Apply, manual);
    }

    private static string NamespaceOf(string text) => NamespaceDeclaration().Match(text) is { Success: true } m ? m.Groups["ns"].Value : string.Empty;

    // ------------------------------------------------------------------------------------------------------------------------------
    // describe_settings

    private sealed record OptionsClass(string Name, string Project, string ProjectDir, string Section, string Body, string Namespace);

    /// <summary>Known options classes; a nested type that is not named ...Options is looked up in the source the first time it is asked for.</summary>
    private sealed class Known(string source) : Dictionary<string, OptionsClass>(StringComparer.Ordinal)
    {
        public bool Find(string name, out OptionsClass found)
        {
            if (TryGetValue(name, out found!))
                return true;
            var declaration = Regex.Match(source, @"\b(?:public|internal)\s+(?:sealed\s+)?(?:class|record)\s+" + Regex.Escape(name) + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            var body = declaration.Success ? Block(source, declaration.Index + declaration.Length) : null;
            if (body != null && Property().IsMatch(body))
            {
                found = new OptionsClass(name, string.Empty, string.Empty, string.Empty, body, string.Empty);
                this[name] = found;
                return true;
            }

            found = null!;
            return false;
        }
    }

    private static OperationResult RunDescribeSettings(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        var ctx = Context(root);
        var manual = new List<string>();
        var classes = new List<OptionsClass>();
        var nestedOptions = new List<(string Name, string Project, string ProjectDir, string Text, Match Match, string Body)>();
        var allSource = string.Join('\n', ctx.Source.Select(p => SourceOf(ctx, p)));
        foreach (var project in ctx.Source.Where(p => p.Layer is not (ProjectLayer.Api or ProjectLayer.Mcp or ProjectLayer.Cli)))
        {
            foreach (var file in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var text = ctx.Read(file, maxBytes: 4 * 1024 * 1024);
                foreach (Match match in OptionsDeclaration().Matches(text))
                {
                    var name = match.Groups["name"].Value;
                    var body = Block(text, match.Index + match.Length);
                    if (body == null || !Property().IsMatch(body))
                        continue;
                    var section = SectionConst().Match(body) is { Success: true } sc ? sc.Groups["value"].Value : SectionBoundTo(allSource, name);
                    if (section == null)
                    {
                        // a class used as the type of a property of another options class is part of that class; the rest is not read from configuration at all
                        var nested = Regex.IsMatch(allSource, @"public\s+[\w<>\[\],?. ]*\b" + Regex.Escape(name) + @"\b[>\[\]?]*\s+\w+\s*\{\s*get;", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                        nestedOptions.Add((name, project.Name, project.Dir, text, match, body));
                        if (!nested)
                            manual.Add($"{project.Name}: {name} has no section name (no SectionName constant, no GetSection bound to it): it is not read from configuration, or it is bound in a way this operation does not follow");
                        continue;
                    }

                    classes.Add(new OptionsClass(name, project.Name, project.Dir, section, body, NamespaceOf(text)));
                }
            }
        }

        var byName = new Known(allSource);
        foreach (var c in classes)
            byName[c.Name] = c;
        foreach (var n in nestedOptions)
            byName.TryAdd(n.Name, new OptionsClass(n.Name, n.Project, n.ProjectDir, string.Empty, n.Body, NamespaceOf(n.Text)));
        var plan = new List<FixAction>();
        foreach (var group in classes.GroupBy(c => c.ProjectDir))
        {
            var members = group.OrderBy(c => c.Section, StringComparer.Ordinal).ToList();
            var properties = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var option in members)
            {
                var path = option.Section.Split(':');
                var cursor = properties;
                for (var i = 0; i < path.Length; i++)
                {
                    if (!cursor.TryGetValue(path[i], out var node) || node is not Dictionary<string, object> existing)
                    {
                        existing = new Dictionary<string, object> { ["type"] = "object" };
                        cursor[path[i]] = existing;
                    }

                    if (i == path.Length - 1)
                    {
                        foreach (var (key, value) in ObjectSchema(option, byName, new HashSet<string>(StringComparer.Ordinal) { option.Name }))
                            existing[key] = value;
                    }
                    else
                    {
                        if (!existing.TryGetValue("properties", out var inner) || inner is not Dictionary<string, object> child)
                        {
                            child = new Dictionary<string, object>(StringComparer.Ordinal);
                            existing["properties"] = child;
                            existing["additionalProperties"] = false;
                        }

                        cursor = child;
                    }
                }
            }

            var schema = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["title"] = $"{group.First().Project} settings",
                ["description"] = "Derived by dotnet-arch describe_settings from the options classes; run it again after a code change.",
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false,
            };
            var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true }) + "\n";
            var file = $"{group.Key}/settings.schema.json";
            plan.Add(new FixAction(new PlannedChange(file, ctx.Has(file) ? "modify" : "create", $"{members.Count} options class(es): {string.Join(", ", members.Select(m => m.Name))}", "describe_settings"), json));
        }

        return Planned("describe_settings", root, plan, request.Apply, manual);
    }

    private static Dictionary<string, object> ObjectSchema(OptionsClass option, Known known, HashSet<string> seen)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        var required = new List<string>();
        foreach (Match match in Property().Matches(option.Body))
        {
            var name = match.Groups["name"].Value;
            var type = match.Groups["type"].Value.Trim();
            var node = TypeSchema(type, known, seen);
            var defaultValue = match.Groups["default"].Success ? Literal(match.Groups["default"].Value.Trim()) : null;
            if (defaultValue != null)
                node["default"] = defaultValue;
            if (Secrets.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase)) && type.TrimEnd('?') == "string")
            {
                node["writeOnly"] = true;
                node["description"] = "A secret: the value is a reference (vault://...), never stored in a composition or in Git.";
            }

            if (match.Groups["required"].Success)
                required.Add(name);
            properties[name] = node;
        }

        var schema = new Dictionary<string, object>(StringComparer.Ordinal) { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        if (required.Count > 0)
            schema["required"] = required;
        return schema;
    }

    private static Dictionary<string, object> TypeSchema(string type, Known known, HashSet<string> seen)
    {
        var bare = type.TrimEnd('?');
        switch (bare)
        {
            case "string":
                return new Dictionary<string, object> { ["type"] = "string" };
            case "int" or "long" or "short" or "byte" or "uint" or "ulong":
                return new Dictionary<string, object> { ["type"] = "integer" };
            case "decimal" or "double" or "float":
                return new Dictionary<string, object> { ["type"] = "number" };
            case "bool":
                return new Dictionary<string, object> { ["type"] = "boolean" };
            case "TimeSpan":
                return new Dictionary<string, object> { ["type"] = "string", ["description"] = "A duration (hh:mm:ss)." };
            case "Guid":
                return new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" };
            case "Uri":
                return new Dictionary<string, object> { ["type"] = "string", ["format"] = "uri" };
        }

        var collection = Collection().Match(bare);
        if (collection.Success)
            return new Dictionary<string, object> { ["type"] = "array", ["items"] = TypeSchema(collection.Groups["item"].Value.Trim(), known, seen) };
        if (bare.EndsWith("[]", StringComparison.Ordinal))
            return new Dictionary<string, object> { ["type"] = "array", ["items"] = TypeSchema(bare[..^2], known, seen) };
        var map = Dictionary().Match(bare);
        if (map.Success)
            return new Dictionary<string, object> { ["type"] = "object", ["additionalProperties"] = TypeSchema(map.Groups["value"].Value.Trim(), known, seen) };
        if (known.Find(bare, out var nested) && seen.Add(bare))
        {
            var result = ObjectSchema(nested, known, seen);
            seen.Remove(bare);
            return result;
        }

        // another class that declares settable properties (a nested options type) is read as an object; an enum or an unknown type is a string
        return new Dictionary<string, object> { ["type"] = "string", ["description"] = $"Value of {bare}." };
    }

    private static object? Literal(string text)
    {
        if (text.StartsWith('"') && text.EndsWith('"') && text.Length >= 2)
            return text[1..^1];
        if (text is "true" or "false")
            return text == "true";
        if (long.TryParse(text.TrimEnd('L', 'l'), out var integer))
            return integer;
        if (double.TryParse(text.TrimEnd('m', 'M', 'd', 'D', 'f', 'F'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            return number;
        return null;
    }

    private static string? SectionBoundTo(string source, string className)
    {
        var configure = Regex.Match(source, @"Configure<\s*" + Regex.Escape(className) + @"\s*>\s*\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        if (!configure.Success)
            return null;
        var window = source.Substring(configure.Index, Math.Min(source.Length - configure.Index, 2500));
        var section = Regex.Match(window, @"GetSection\(\s*""(?<value>[^""]+)""\s*\)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        if (section.Success)
            return section.Groups["value"].Value;
        // section.Bind(opts) where the section is a local declared just before: var section = configuration.GetSection("X");
        var bound = Regex.Match(window, @"(?<id>\b[a-z]\w*)\.Bind\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        if (!bound.Success || bound.Groups["id"].Value == "configuration")
            return null;
        var before = source[..configure.Index];
        var declaration = Regex.Matches(before, @"\b" + Regex.Escape(bound.Groups["id"].Value) + @"\s*=\s*[\w.]*GetSection\(\s*""(?<value>[^""]+)""\s*\)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        return declaration.Count > 0 ? declaration[^1].Groups["value"].Value : null;
    }

    /// <summary>The text between the braces of the block that starts at <paramref name="from"/> (the first opening brace after it).</summary>
    private static string? Block(string text, int from)
    {
        var open = text.IndexOf('{', from);
        if (open < 0)
            return null;
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}' && --depth == 0)
                return text.Substring(open + 1, i - open - 1);
        }

        return null;
    }

    [GeneratedRegex(@"(?m)^\s*namespace\s+(?<ns>[A-Za-z_][\w.]*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex NamespaceDeclaration();

    [GeneratedRegex(@"public\s+interface\s+(?<name>I[A-Za-z0-9_]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PortInterface();

    [GeneratedRegex(@"<Version>(?<v>[^<]+)</Version>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex VersionElement();

    [GeneratedRegex(@"<PackageId>(?<id>[^<]+)</PackageId>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PackageIdElement();

    [GeneratedRegex(@"(?<lead>(?:\[Obsolete[^\]]*\]\s*)?)public\s+static\s+IServiceCollection\s+(?<name>Add\w+)\s*\(\s*this\s+IServiceCollection", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Registrations();

    [GeneratedRegex(@"\b(?:class|record)\s+\w+(?:\s*\([^)]*\))?\s*:\s*(?<bases>[^{;]+?)\s*(?:\{|;|where\b)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ClassBases();

    [GeneratedRegex(@"(?:configuration|section|config)\s*(?:\[\s*""(?<key>[^""]+)""\s*\]|\.GetSection\(\s*""(?<key>[^""]+)""\s*\)|\.GetValue<[^>]+>\(\s*""(?<key>[^""]+)""\s*\))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ConfigurationKey();

    [GeneratedRegex(@"AdapterKey\s*=\s*""(?<key>[^""]+)""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SelectorKey();

    [GeneratedRegex(@"\b(?:public|internal)\s+(?:sealed\s+)?(?:class|record)\s+(?<name>\w*Options)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex OptionsDeclaration();

    [GeneratedRegex(@"SectionName\s*=\s*""(?<value>[^""]+)""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SectionConst();

    [GeneratedRegex(@"public\s+(?<required>required\s+)?(?<type>[\w<>\[\],?. ]+?)\s+(?<name>\w+)\s*\{\s*get;\s*(?:set|init);\s*\}(?:\s*=\s*(?<default>[^;]+);)?", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Property();

    [GeneratedRegex(@"^(?:List|IList|IEnumerable|IReadOnlyList|IReadOnlyCollection|ICollection|HashSet|ISet)<(?<item>.+)>$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Collection();

    [GeneratedRegex(@"^(?:Dictionary|IDictionary|IReadOnlyDictionary)<\s*string\s*,\s*(?<value>.+)>$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Dictionary();
}
