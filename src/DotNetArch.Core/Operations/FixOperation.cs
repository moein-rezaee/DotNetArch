using DotNetArch.Core.Doctor;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Restores the standard where the fix is mechanical and cannot change behaviour: it only creates missing hygiene files from the tool's own
/// templates or appends missing ignore lines. Business code is never touched (D-21). Plan first; writes only with apply.
/// </summary>
internal static class FixOperation
{
    public static readonly OperationDefinition Definition = new(
        "fix",
        "Fix doctor findings that are safe and mechanical (missing global.json, .editorconfig, .gitignore lines, .dockerignore). Never changes source code. Plan first; writes only with apply. Other findings are listed as manual.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true),
            new OperationParameter("profile", "Profile file to apply instead of .net-arch/profile.yml."),
            new OperationParameter("rules", "Comma-separated rule ids to fix (default: every fixable finding)."),
        },
        Run);

    private static OperationResult Run(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        var report = DoctorRunner.Run(root, new DoctorOptions(request.Get("profile")));
        var only = request.Get("rules")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plan = new List<(PlannedChange Change, string Content)>();
        var manual = new List<string>();

        foreach (var finding in report.Findings.Where(f => f.Severity != DoctorSeverity.Info))
        {
            if (only != null && !only.Contains(finding.Id))
                continue;
            var fix = Fixer(root, finding);
            if (fix == null)
                manual.Add($"{finding.Id}  {finding.Message}");
            else
                plan.Add(fix.Value);
        }

        if (request.Apply)
        {
            foreach (var (change, content) in plan)
            {
                var path = Path.Combine(root, change.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }
        }

        var lines = plan.Select(p => $"  {p.Change.Action,-7} {p.Change.Path}  [{p.Change.RuleId}] {p.Change.Reason}").ToList();
        var text = $"fix: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {plan.Count} change(s), {manual.Count} manual finding(s)\n"
                   + string.Join("\n", lines)
                   + (manual.Count == 0 ? string.Empty : "\nmanual (not mechanical):\n" + string.Join("\n", manual.Select(m => "  " + m)));
        return new OperationResult(true, text, new { manual }, plan.Select(p => p.Change).ToList(), request.Apply);
    }

    private static (PlannedChange, string)? Fixer(string root, DoctorFinding finding)
    {
        switch (finding.Id)
        {
            case "DA-B01":
                var sdk = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Take(50).Any(f => File.ReadAllText(f).Contains("net9.0", StringComparison.Ordinal)) ? "9.0.100" : "8.0.100";
                return (new PlannedChange("global.json", "create", "pin the SDK version", finding.Id),
                        TemplateRenderer.RenderTemplate("V2/solution/global.json.tpl", new Dictionary<string, string> { ["SdkVersion"] = sdk }));
            case "DA-B05":
                return (new PlannedChange(".editorconfig", "create", "shared code-style rules", finding.Id), TemplateRenderer.Load("V2/solution/.editorconfig.tpl"));
            case "DA-B08":
                return (new PlannedChange(".gitignore", "create", "ignore build output, IDE files and secrets", finding.Id), TemplateRenderer.Load("V2/solution/.gitignore.tpl"));
            case "DA-B09":
                var current = File.ReadAllText(Path.Combine(root, ".gitignore"));
                var existing = current.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
                var add = new[] { "bin/", "obj/", ".env" }.Where(l => !existing.Contains(l) && !existing.Contains("**/" + l) && !existing.Contains("/" + l) && !existing.Contains(l.TrimEnd('/'))).ToList();
                return add.Count == 0 ? null : (new PlannedChange(".gitignore", "modify", $"append: {string.Join(", ", add)}", finding.Id), current.TrimEnd('\n') + "\n" + string.Join("\n", add) + "\n");
            case "DA-D04":
                return (new PlannedChange(".dockerignore", "create", "keep host bin/obj out of the build context", finding.Id), TemplateRenderer.Load("V2/ops/dockerignore.tpl"));
            default:
                return null;
        }
    }
}
