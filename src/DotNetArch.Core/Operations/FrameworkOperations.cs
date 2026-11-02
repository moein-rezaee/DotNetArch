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
        },
        request =>
        {
            var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
            if (!Directory.Exists(root))
                throw new ArgumentException($"Folder not found: {root}");
            if (!int.TryParse(request.Get("target"), out var target) || target < Oldest)
                throw new ArgumentException($"The target must be a .NET major version of {Oldest} or higher, for example 10.");

            var ctx = new RepoContext(root, NetArchStore.ResolveProfile(root, null, out _), new RuleSettings(), Enumerable.Empty<string>());
            var plan = Plan(ctx, target, out var manual);
            if (request.Apply)
                PlanApplier.Apply(root, plan);

            var changes = plan.Select(p => p.Change).ToList();
            var text = $"migrate_framework: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {changes.Count} change(s) to net{target}.0\n"
                       + string.Join("\n", changes.Select(c => $"  {c.Action,-7} {c.Path}  {c.Reason}"))
                       + (manual.Count == 0 ? string.Empty : "\nmanual:\n" + string.Join("\n", manual.Select(m => "  " + m)));
            return new OperationResult(true, text, new { target = $"net{target}.0", manual }, changes, request.Apply);
        });

    internal static IReadOnlyList<FixAction> Plan(RepoContext ctx, int target, out List<string> manual)
    {
        manual = new List<string>();
        var actions = new List<FixAction>();
        var newest = 0;

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

                var packages = MoveRuntimePackages(updated, target, manual, file);
                if (packages != updated) { updated = packages; reasons.Add($"runtime packages move to {target}.0.0"); }
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
            manual.Add($"Install the .NET {target} SDK before building; then run restore, build and the tests, and update the changelog.");

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
    private static string MoveRuntimePackages(string text, int target, List<string> manual, string file) =>
        PackageVersion().Replace(text, m =>
        {
            var id = m.Groups["id"].Value;
            var major = m.Groups["major"].Value;
            if (!Below(major, target))
                return m.Value;
            if (RuntimePackagePrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal)))
                return $"{m.Groups["head"].Value}{target}.0.0\"";
            if (ThirdPartyCoupled.Contains(id, StringComparer.Ordinal))
                manual.Add($"{file}: {id} {m.Groups["major"].Value}.x follows the runtime; move it to a version built for net{target}.0.");
            return m.Value;
        });

    [GeneratedRegex(@"(?<![\w.])net(?<major>\d{1,2})\.0(?![\d.\w-])", RegexOptions.CultureInvariant)]
    private static partial Regex FrameworkToken();

    [GeneratedRegex("\"version\"\\s*:\\s*\"(?<major>\\d{1,2})\\.\\d+\\.\\d+[^\"]*\"", RegexOptions.CultureInvariant)]
    private static partial Regex SdkPin();

    [GeneratedRegex(@"(?<image>(?:[\w.${}-]+(?::\d+)?/)*dotnet/(?:nightly/)?(?:sdk|aspnet|runtime|runtime-deps)):(?<major>\d{1,2})\.\d+(?:\.\d+)?(?=[-\s""'$]|$)", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ImageTag();

    [GeneratedRegex(@"(?<head><Package(?:Version|Reference)\s+Include=""(?<id>[^""]+)""\s+Version="")(?<major>\d{1,2})\.\d+\.\d+[^""]*""", RegexOptions.CultureInvariant)]
    private static partial Regex PackageVersion();
}
