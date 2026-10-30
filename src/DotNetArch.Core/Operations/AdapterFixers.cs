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
    public static readonly HashSet<string> RuleIds = new(StringComparer.OrdinalIgnoreCase) { "DA-AD01", "DA-AD02", "DA-AD03", "DA-AD05", "DA-AD06" };

    public static IReadOnlyList<FixAction> Plan(string rule, RepoContext ctx, IList<string> manual) => rule.ToUpperInvariant() switch
    {
        "DA-AD01" => RemoveUnused(ctx, manual, rule, AdapterChecks.Adapters(ctx), (a, t) => AdapterChecks.SystemOf(ctx, t) == null && t.Layer is ProjectLayer.Application or ProjectLayer.Infrastructure or ProjectLayer.Api or ProjectLayer.Mcp or ProjectLayer.Cli or ProjectLayer.HttpApi or ProjectLayer.HttpApiClient),
        "DA-AD02" => RemoveUnused(ctx, manual, rule, AdapterChecks.Host(ctx) is { } host ? new[] { host } : Array.Empty<ProjectInfo>(), (_, t) => AdapterChecks.SystemOf(ctx, t) != null),
        "DA-AD03" => RemoveUnused(ctx, manual, rule, AdapterChecks.Adapters(ctx), (_, t) => AdapterChecks.SystemOf(ctx, t) != null),
        "DA-AD05" => Package(ctx, manual),
        "DA-AD06" => ShipSchema(ctx),
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

        var actions = new List<FixAction>();
        foreach (var adapter in AdapterChecks.Adapters(ctx))
        {
            var text = ctx.Read(adapter.File);
            if (text.Contains("<PackageId>", StringComparison.Ordinal))
                continue;
            var app = adapter.Name[..adapter.Name.IndexOf(".Infrastructure.", StringComparison.Ordinal)];
            var module = app.EndsWith("Service", StringComparison.Ordinal) ? app[..^"Service".Length] : app;
            var system = AdapterChecks.SystemOf(ctx, adapter)!;
            var id = pattern.Replace("{Module}", module, StringComparison.Ordinal).Replace("{System}", system, StringComparison.Ordinal);
            var group = text.IndexOf("</PropertyGroup>", StringComparison.Ordinal);
            if (group < 0)
                continue;
            var lines = $"    <IsPackable>true</IsPackable>\n    <PackageId>{id}</PackageId>\n    <Description>The {system} adapter of the {module} module.</Description>\n  ";
            actions.Add(new FixAction(new PlannedChange(adapter.File, "modify", $"package {id}", "DA-AD05"), text.Insert(group, lines)));
        }

        return actions;
    }

    [GeneratedRegex(@"(?m)^\s*(?:global\s+)?using\s+(?:static\s+)?(?<ns>[A-Za-z_][\w.]*)\s*;", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UsingNamespace();

    [GeneratedRegex(@"(?m)^[ \t]*<ItemGroup>\s*</ItemGroup>[ \t]*\r?\n(?:[ \t]*\r?\n)?", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex EmptyItemGroups();
}
