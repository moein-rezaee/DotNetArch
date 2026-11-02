using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>migrate_framework</c>: moves a repository to a newer .NET version in one plan. The target is an input (8, 9, 10 and later), nothing is hard-coded.
/// Only the version moves: target frameworks (and the <c>netX.0</c> paths that follow them), the SDK pin in <c>global.json</c>, the .NET image tags in
/// Dockerfiles, pipelines and Compose files, and the packages that ship with the runtime (<c>Microsoft.Extensions.*</c>, <c>Microsoft.AspNetCore.*</c>,
/// <c>Microsoft.EntityFrameworkCore*</c>, <c>System.Text.Json</c>). Third-party packages tied to the runtime are listed for a person to move. A repository
/// already on (or above) the target is left alone, so the operation is safe to run again.
/// </summary>
internal static partial class FrameworkOperations
{
    /// <summary>The oldest version the operation moves from or to: before it the target-framework names have a different shape.</summary>
    private const int Oldest = 6;

    /// <summary>From this version the SDK reports a package the framework already provides as NU1510 (an error where warnings are errors).</summary>
    private const int PruningFrom = 10;

    private static readonly string[] RuntimePackagePrefixes = { "Microsoft.Extensions.", "Microsoft.AspNetCore.", "Microsoft.EntityFrameworkCore", "System.Text.Json" };

    private static readonly string[] ThirdPartyCoupled = { "Npgsql.EntityFrameworkCore.PostgreSQL", "Pomelo.EntityFrameworkCore.MySql", "Npgsql", "Swashbuckle.AspNetCore", "Microsoft.AspNetCore.OpenApi" };

    public static IReadOnlyList<OperationDefinition> All => new[] { MigrateFramework };

    public static OperationDefinition MigrateFramework { get; } = new(
        "migrate_framework",
        "Move the repository to a newer .NET version: target frameworks, the SDK pin, the .NET image tags in Dockerfiles, pipelines and Compose files, and the packages that ship with the runtime. The target version is an input (8, 9, 10 and later). Third-party packages tied to the runtime are listed, not changed. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true, Default: "@cwd"),
            new OperationParameter("target", "The .NET major version to move to, for example 10.", Required: true, Choices: new[] { "8", "9", "10" }),
            new OperationParameter("packages", "Exact versions for packages the migration cannot derive, as Id=Version separated by commas (for example the EF Core database provider built for the new runtime). They are applied wherever the package is referenced."),
            new OperationParameter("dotnet_root", "Folder of an installed .NET SDK of the target version; its package list decides which references the framework now provides (NU1510, .NET 10 and later). Default: found from DOTNET_ROOT, the dotnet on the path and the usual folders."),
        },
        request =>
        {
            var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
            if (!Directory.Exists(root))
                throw new ArgumentException($"Folder not found: {root}");
            if (!int.TryParse(request.Get("target"), out var target) || target < Oldest)
                throw new ArgumentException($"The target must be a .NET major version of {Oldest} or higher, for example 10.");

            var ctx = new RepoContext(root, NetArchStore.ResolveProfile(root, null, out _), new RuleSettings(), Enumerable.Empty<string>());
            var plan = Plan(ctx, target, request.Get("dotnet_root"), ParsePins(request.Get("packages")), out var manual);
            if (request.Apply)
                PlanApplier.Apply(root, plan);

            var changes = plan.Select(p => p.Change).ToList();
            var text = $"migrate_framework: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {changes.Count} change(s) to net{target}.0\n"
                       + string.Join("\n", changes.Select(c => $"  {c.Action,-7} {c.Path}  {c.Reason}"))
                       + (manual.Count == 0 ? string.Empty : "\nmanual:\n" + string.Join("\n", manual.Select(m => "  " + m)));
            return new OperationResult(true, text, new { target = $"net{target}.0", manual }, changes, request.Apply);
        });

    internal static IReadOnlyDictionary<string, string> ParsePins(string? text)
    {
        var pins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || pair[0].Length == 0 || pair[1].Length == 0)
                throw new ArgumentException($"A package must be written Id=Version, got '{part}'.");
            pins[pair[0]] = pair[1];
        }

        return pins;
    }

    internal static IReadOnlyList<FixAction> Plan(RepoContext ctx, int target, string? dotnetRoot, IReadOnlyDictionary<string, string> pins, out List<string> manual)
    {
        manual = new List<string>();
        var actions = new List<FixAction>();
        var newest = 0;
        var overrides = target >= PruningFrom ? FrameworkPackages.Find(target, dotnetRoot) : null;
        var web = target >= PruningFrom ? WebProjects(ctx) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var centralVersions = CentralVersions(ctx);
        if (target >= PruningFrom && overrides is null)
            manual.Add($"The .NET {target} SDK was not found, so packages the framework now provides (NU1510) were not removed. Install it (or pass dotnet_root) and run this again.");

        foreach (var file in ctx.Files)
        {
            var name = Path.GetFileName(file);
            var text = ctx.Read(file);
            var updated = text;
            var reasons = new List<string>();

            if (IsProjectFile(file))
            {
                foreach (Match m in FrameworkToken().Matches(text))
                    newest = Math.Max(newest, int.Parse(m.Groups["major"].Value));
                var moved = FrameworkToken().Replace(updated, m => Below(m.Groups["major"].Value, target) ? $"net{target}.0" : m.Value);
                if (moved != updated) { updated = moved; reasons.Add($"target framework moves to net{target}.0"); }

                var packages = MoveRuntimePackages(updated, target, pins, manual, file);
                if (packages != updated) { updated = packages; reasons.Add($"runtime packages move to {target}.0.0"); }

                if (overrides is not null && file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    var pruned = PruneProvided(updated, overrides, web.Contains(file), centralVersions, manual, file);
                    if (pruned != updated) { updated = pruned; reasons.Add("references the framework now provides are removed (NU1510)"); }
                }
            }
            else if (name.Equals("global.json", StringComparison.OrdinalIgnoreCase))
            {
                var moved = SdkPin().Replace(updated, m => Below(m.Groups["major"].Value, target) ? $"\"version\": \"{target}.0.100\"" : m.Value);
                if (moved != updated) { updated = moved; reasons.Add($"SDK pin moves to {target}.0.100"); }
            }
            else if (IsBuildFile(file))
            {
                var moved = ImageTag().Replace(updated, m => Below(m.Groups["major"].Value, target) ? $"{m.Groups["image"].Value}:{target}.0" : m.Value);
                moved = FrameworkToken().Replace(moved, m => Below(m.Groups["major"].Value, target) ? $"net{target}.0" : m.Value);
                if (moved != updated) { updated = moved; reasons.Add($".NET image tags and netX.0 paths move to {target}.0"); }
            }

            if (updated != text)
                actions.Add(new FixAction(new PlannedChange(file, "modify", string.Join("; ", reasons)), updated));
        }

        if (actions.Count == 0)
            manual.Add(newest >= target
                ? $"Nothing to change: the repository is already on net{newest}.0 (target net{target}.0)."
                : "Nothing to change: no target framework below the target was found.");
        else
            manual.Add($"Install the .NET {target} SDK before building; then run restore, build and the tests. Source that no longer compiles on the new language version (for example a call that became ambiguous between overloads) is a code change for a person; then update the changelog.");

        return actions;
    }

    private static bool Below(string major, int target) => int.Parse(major) is var v && v >= Oldest && v < target;

    private static bool IsProjectFile(string file) =>
        file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildFile(string file)
    {
        var name = Path.GetFileName(file);
        var lower = file.ToLowerInvariant();
        return name.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".dockerfile", StringComparison.OrdinalIgnoreCase)
               || lower.EndsWith(".yml", StringComparison.Ordinal)
               || lower.EndsWith(".yaml", StringComparison.Ordinal)
               || lower.EndsWith(".sh", StringComparison.Ordinal);
    }

    /// <summary>Moves the runtime-shipped packages whose major version is below the target; lists third-party packages that follow the runtime.</summary>
    private static string MoveRuntimePackages(string text, int target, IReadOnlyDictionary<string, string> pins, List<string> manual, string file) =>
        PackageVersion().Replace(text, m =>
        {
            var id = m.Groups["id"].Value;
            var major = m.Groups["major"].Value;
            if (pins.TryGetValue(id, out var pinned))
                return $"{m.Groups["head"].Value}{pinned}\"";
            if (!Below(major, target))
                return m.Value;
            if (RuntimePackagePrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal)))
                return $"{m.Groups["head"].Value}{target}.0.0\"";
            if (ThirdPartyCoupled.Contains(id, StringComparer.Ordinal))
                manual.Add($"{file}: {id} {m.Groups["major"].Value}.x follows the runtime; move it to a version built for net{target}.0.");
            return m.Value;
        });

    /// <summary>Projects that use the ASP.NET Core shared framework: a Web SDK project or a framework reference, and every project that references one.</summary>
    private static HashSet<string> WebProjects(RepoContext ctx)
    {
        var projects = ctx.Files.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToList();
        var web = projects.Where(f => ctx.Read(f) is var t && (t.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal) || t.Contains("FrameworkReference Include=\"Microsoft.AspNetCore.App\"", StringComparison.Ordinal)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = projects.ToDictionary(f => f, f => ProjectReferenceMatch().Matches(ctx.Read(f))
            .Select(m => NormalizePath(Path.GetDirectoryName(f) ?? string.Empty, m.Groups["path"].Value)).ToList(), StringComparer.OrdinalIgnoreCase);
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var project in projects)
                if (!web.Contains(project) && references[project].Any(web.Contains))
                    changed = web.Add(project) || changed;
        }

        return web;
    }

    private static string NormalizePath(string baseDir, string reference)
    {
        var parts = new List<string>(baseDir.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var part in reference.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part != ".") parts.Add(part);
        }

        return string.Join('/', parts);
    }

    private static Dictionary<string, string> CentralVersions(RepoContext ctx)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in ctx.Files.Where(f => Path.GetFileName(f).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)))
            foreach (Match m in CentralPackage().Matches(ctx.Read(file)))
                versions[m.Groups["id"].Value] = m.Groups["version"].Value;
        return versions;
    }

    /// <summary>Removes a single-line package reference the framework of the project already provides at the same or a higher version.</summary>
    private static string PruneProvided(string text, FrameworkPackages provided, bool web, IReadOnlyDictionary<string, string> central, List<string> manual, string file) =>
        SelfClosingReference().Replace(text, m =>
        {
            var id = m.Groups["id"].Value;
            if (!provided.Provides(id, web, out var shipped))
                return m.Value;
            var version = m.Groups["version"].Success ? m.Groups["version"].Value : central.GetValueOrDefault(id);
            return FrameworkPackages.IsAtMost(version, shipped) ? string.Empty : m.Value;
        });

    [GeneratedRegex(@"<ProjectReference\s+Include=""(?<path>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectReferenceMatch();

    [GeneratedRegex(@"<PackageVersion\s+Include=""(?<id>[^""]+)""\s+Version=""(?<version>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex CentralPackage();

    [GeneratedRegex(@"^[ \t]*<PackageReference\s+Include=""(?<id>[^""]+)""(?:\s+Version=""(?<version>[^""]+)"")?\s*/>[ \t]*\r?\n", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex SelfClosingReference();

    [GeneratedRegex(@"(?<![\w.])net(?<major>\d{1,2})\.0(?![\d.\w-])", RegexOptions.CultureInvariant)]
    private static partial Regex FrameworkToken();

    [GeneratedRegex("\"version\"\\s*:\\s*\"(?<major>\\d{1,2})\\.\\d+\\.\\d+[^\"]*\"", RegexOptions.CultureInvariant)]
    private static partial Regex SdkPin();

    [GeneratedRegex(@"(?<image>(?:[\w.${}-]+(?::\d+)?/)*dotnet/(?:nightly/)?(?:sdk|aspnet|runtime|runtime-deps)):(?<major>\d{1,2})\.\d+(?:\.\d+)?(?=[-\s""'$]|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ImageTag();

    [GeneratedRegex(@"(?<head><Package(?:Version|Reference)\s+Include=""(?<id>[^""]+)""\s+(?:Version|VersionOverride)="")(?<major>\d{1,2})\.\d+\.\d+[^""]*""", RegexOptions.CultureInvariant)]
    private static partial Regex PackageVersion();
}
