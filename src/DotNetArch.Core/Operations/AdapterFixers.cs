using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Fixes for the <c>adapters</c> rule set. A reference nothing uses is removed (the build cannot change); a used one is listed as manual because the type
/// belongs in the module core. The package id comes from the profile pattern <c>adapter_package</c> (tokens <c>{Module}</c> and <c>{System}</c>): the tool names
/// no organisation itself.
/// </summary>
internal static partial class AdapterFixers
{
    /// <summary>Inserts property lines before the closing tag of the PropertyGroup at <paramref name="group"/>. A group written on one line gets the properties inline, so the file stays valid.</summary>
    private static string InsertIntoGroup(string text, int group, string lines, string closingIndent)
    {
        var lineStart = text.LastIndexOf('\n', group) + 1;
        if (string.IsNullOrWhiteSpace(text[lineStart..group]))
            return text.Insert(lineStart, lines + closingIndent);
        return text.Insert(group, lines.Replace("\n", string.Empty, StringComparison.Ordinal).Replace("    ", string.Empty, StringComparison.Ordinal));
    }

    public static readonly HashSet<string> RuleIds = new(StringComparer.OrdinalIgnoreCase) { "DA-AD01", "DA-AD02", "DA-AD03", "DA-AD05", "DA-AD06", "DA-AD08" };

    public static IReadOnlyList<FixAction> Plan(string rule, RepoContext ctx, IList<string> manual) => rule.ToUpperInvariant() switch
    {
        "DA-AD01" => RemoveUnused(ctx, manual, rule, AdapterChecks.Adapters(ctx), (a, t) => AdapterChecks.SystemOf(ctx, t) == null && t.Layer is ProjectLayer.Application or ProjectLayer.Infrastructure or ProjectLayer.Api or ProjectLayer.Mcp or ProjectLayer.Cli or ProjectLayer.HttpApi or ProjectLayer.HttpApiClient),
        "DA-AD02" => RemoveUnused(ctx, manual, rule, AdapterChecks.Host(ctx) is { } host ? new[] { host } : Array.Empty<ProjectInfo>(), (_, t) => AdapterChecks.SystemOf(ctx, t) != null),
        "DA-AD03" => RemoveUnused(ctx, manual, rule, AdapterChecks.Adapters(ctx), (_, t) => AdapterChecks.SystemOf(ctx, t) != null),
        "DA-AD05" => Package(ctx, manual),
        "DA-AD06" => ShipSchema(ctx),
        "DA-AD08" => PackageCore(ctx, manual),
        _ => Array.Empty<FixAction>(),
    };

    private static IReadOnlyList<FixAction> RemoveUnused(RepoContext ctx, IList<string> manual, string rule, IEnumerable<ProjectInfo> owners, Func<ProjectInfo, ProjectInfo, bool> forbidden)
    {
        var actions = new List<FixAction>();
        var propsText = ctx.Read("Directory.Packages.props");
        var propsEdited = false;
        var centrally = propsText.Contains("ManagePackageVersionsCentrally", StringComparison.OrdinalIgnoreCase) && propsText.Contains(">true<", StringComparison.OrdinalIgnoreCase);
        foreach (var owner in owners)
        {
            var text = ctx.Read(owner.File);
            var changed = text;
            var removed = new List<ProjectInfo>();
            foreach (var reference in owner.ProjectReferences)
            {
                var target = AdapterChecks.Target(ctx, reference);
                if (target == null || !forbidden(owner, target))
                    continue;
                if (AdapterChecks.Uses(ctx, owner, target))
                {
                    manual.Add($"{rule}  {owner.Name} -> {target.Name} is used; move the type it needs into the module core (move_code), then run this fix again");
                    continue;
                }

                var line = new Regex(@"(?m)^[ \t]*<ProjectReference\s+Include=""[^""]*[\\/]" + Regex.Escape(target.Name) + @"\.csproj""\s*/>[ \t]*\r?\n", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                changed = line.Replace(changed, string.Empty, 1);
                removed.Add(target);
            }

            if (removed.Count == 0)
                continue;

            // what the removed projects brought in transitively and the owner still uses becomes a direct package reference
            var remaining = owner.ProjectReferences.Select(r => AdapterChecks.Target(ctx, r)).Where(t => t != null && !removed.Contains(t!)).Select(t => t!).ToList();
            var stillProvided = new HashSet<string>(StringComparer.Ordinal);
            foreach (var other in remaining)
                foreach (var ns in AdapterOperations.NamespaceIndex(Path.Combine(ctx.Root, other.Dir)).Keys)
                    stillProvided.Add(ns);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in ctx.Files.Where(f => f.StartsWith(owner.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                foreach (Match match in UsingNamespace().Matches(ctx.Read(file)))
                    used.Add(match.Groups["ns"].Value);
            var added = new List<string>();
            var resolvedByOwner = AdapterOperations.NamespaceIndex(Path.Combine(ctx.Root, owner.Dir));
            foreach (var target in removed)
            {
                foreach (var (ns, library) in AdapterOperations.NamespaceIndex(Path.Combine(ctx.Root, target.Dir)).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!used.Contains(ns) || stillProvided.Contains(ns) || ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal))
                        continue;
                    if (owner.Packages.Any(p => p.Name.Equals(library.Id, StringComparison.OrdinalIgnoreCase)) || changed.Contains($"PackageReference Include=\"{library.Id}\"", StringComparison.OrdinalIgnoreCase))
                        continue;
                    changed = AdapterOperations.AddItem(changed, "PackageReference", library.Id);
                    added.Add(library.Id);
                    if (centrally && !propsText.Contains($"Include=\"{library.Id}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        // the version the owner resolves today (its own restore), so a direct reference never lowers it
                        var version = resolvedByOwner.TryGetValue(ns, out var own) && own.Id.Equals(library.Id, StringComparison.OrdinalIgnoreCase) ? own.Version : library.Version;
                        propsText = AdapterOperations.AddItem(propsText, "PackageVersion", library.Id, $" Version=\"{version}\"");
                        propsEdited = true;
                    }
                }
            }

            var reason = $"no longer references {string.Join(", ", removed.Select(r => r.Name))}" + (added.Count == 0 ? string.Empty : $"; now references {string.Join(", ", added)} directly");
            actions.Add(new FixAction(new PlannedChange(owner.File, "modify", reason, rule), EmptyItemGroups().Replace(changed, string.Empty)));
        }

        if (propsEdited)
            actions.Add(new FixAction(new PlannedChange("Directory.Packages.props", "modify", "central versions for the packages now referenced directly", rule), propsText));
        return actions;
    }

    private static IReadOnlyList<FixAction> PackageCore(RepoContext ctx, IList<string> manual)
    {
        var pattern = ctx.Profile?.ModulePackage;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            manual.Add("DA-AD08  the profile has no module_package pattern (for example \"<Org>.{Module}.{Layer}\"); the tool names no organisation");
            return Array.Empty<FixAction>();
        }

        var version = AdapterChecks.ModuleVersion(ctx);
        if (version == null)
        {
            manual.Add("DA-AD08  the Api project has no <Version>, so there is no module version to give the core packages");
            return Array.Empty<FixAction>();
        }

        var actions = new List<FixAction>();
        foreach (var core in AdapterChecks.CoreOfAdapters(ctx))
        {
            var text = ctx.Read(core.File);
            var app = core.Name[..core.Name.LastIndexOf('.')];
            var layer = core.Layer switch
            {
                ProjectLayer.Domain => "Domain",
                ProjectLayer.DomainShared => "Domain.Shared",
                ProjectLayer.ApplicationContracts => "Application.Contracts",
                _ => "Contracts",
            };
            // Domain.Shared and Application.Contracts end in two segments
            app = core.Name.EndsWith("." + layer, StringComparison.Ordinal) ? core.Name[..^(layer.Length + 1)] : app;
            var module = app.EndsWith("Service", StringComparison.Ordinal) ? app[..^"Service".Length] : app;
            var id = pattern.Replace("{Module}", module, StringComparison.Ordinal).Replace("{Layer}", layer, StringComparison.Ordinal);
            var changed = text;
            if (!changed.Contains("<PackageId>", StringComparison.Ordinal))
            {
                var group = changed.IndexOf("</PropertyGroup>", StringComparison.Ordinal);
                if (group < 0)
                    continue;
                changed = InsertIntoGroup(changed, group, $"    <IsPackable>true</IsPackable>\n    <PackageId>{id}</PackageId>\n    <Version>{version}</Version>\n    <Description>The {layer} of the {module} module.</Description>\n", "  ");
            }
            else if (!changed.Contains($"<Version>{version}</Version>", StringComparison.Ordinal))
            {
                changed = Regex.Replace(changed, "<Version>[^<]*</Version>", $"<Version>{version}</Version>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (!changed.Contains($"<Version>{version}</Version>", StringComparison.Ordinal))
                {
                    var group = changed.IndexOf("</PropertyGroup>", StringComparison.Ordinal);
                    if (group >= 0)
                        changed = changed.Insert(changed.LastIndexOf('\n', group) + 1, $"    <Version>{version}</Version>\n");
                }
            }

            if (changed != text)
                actions.Add(new FixAction(new PlannedChange(core.File, "modify", $"package {id} {version}", "DA-AD08"), changed));
        }

        return actions;
    }

    private static IReadOnlyList<FixAction> ShipSchema(RepoContext ctx)
    {
        var actions = new List<FixAction>();
        foreach (var adapter in AdapterChecks.Adapters(ctx))
        {
            var text = ctx.Read(adapter.File);
            if (!ctx.Has($"{adapter.Dir}/settings.schema.json") || text.Contains("settings.schema.json", StringComparison.Ordinal))
                continue;
            var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
            if (close < 0)
                continue;
            var item = "\n  <ItemGroup>\n    <None Include=\"settings.schema.json\" Pack=\"true\" PackagePath=\"schemas/\" />\n  </ItemGroup>\n";
            actions.Add(new FixAction(new PlannedChange(adapter.File, "modify", "the package carries settings.schema.json", "DA-AD06"), text.Insert(close, item.TrimStart('\n').Length > 0 ? item : item)));
        }

        return actions;
    }

    private static IReadOnlyList<FixAction> Package(RepoContext ctx, IList<string> manual)
    {
        var pattern = ctx.Profile?.AdapterPackage;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            manual.Add("DA-AD05  the profile has no adapter_package pattern (for example \"<Org>.{Module}.Adapters.{System}\"); the tool names no organisation");
            return Array.Empty<FixAction>();
        }

        // an adapter has its own version, taken from its own tag by CI as AdapterVersion; the core projects it references keep the module version
        const string versionLines = "    <Version>1.0.0</Version>\n    <Version Condition=\"'$(AdapterVersion)' != ''\">$(AdapterVersion)</Version>\n";
        var actions = new List<FixAction>();
        foreach (var adapter in AdapterChecks.Adapters(ctx))
        {
            var text = ctx.Read(adapter.File);
            var group = text.IndexOf("</PropertyGroup>", StringComparison.Ordinal);
            if (group < 0)
                continue;
            var changed = text;
            var reasons = new List<string>();
            if (!changed.Contains("<PackageId>", StringComparison.Ordinal))
            {
                var app = AdapterChecks.AppOf(adapter.Name);
                var module = app.EndsWith("Service", StringComparison.Ordinal) ? app[..^"Service".Length] : app;
                var system = AdapterChecks.SystemOf(ctx, adapter)!;
                var id = pattern.Replace("{Module}", module, StringComparison.Ordinal).Replace("{System}", system, StringComparison.Ordinal);
                changed = InsertIntoGroup(changed, group, $"    <IsPackable>true</IsPackable>\n    <PackageId>{id}</PackageId>\n    <Description>The {system} adapter of the {module} module.</Description>\n", string.Empty);
                reasons.Add($"package {id}");
            }

            if (!changed.Contains("AdapterVersion", StringComparison.Ordinal))
            {
                var current = changed.IndexOf("</PropertyGroup>", StringComparison.Ordinal);
                changed = InsertIntoGroup(changed, current, versionLines, string.Empty);
                reasons.Add("version from the adapter tag");
            }

            if (changed != text)
                actions.Add(new FixAction(new PlannedChange(adapter.File, "modify", string.Join(", ", reasons), "DA-AD05"), changed));
        }

        return actions;
    }

    [GeneratedRegex(@"(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?(?<ns>[A-Za-z_][\w.]*)\s*;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UsingNamespace();

    [GeneratedRegex(@"(?m)^[ \t]*<ItemGroup>\s*</ItemGroup>[ \t]*\r?\n(?:[ \t]*\r?\n)?", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex EmptyItemGroups();
}
