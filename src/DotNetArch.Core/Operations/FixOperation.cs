using DotNetArch.Core.Doctor;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Restores the standard where the fix cannot change behaviour (D-21). By default only hygiene files are created from the tool's own templates or
/// missing ignore lines appended. Structural fixers (central packages, warnings as errors, Domain test project) run only when named in
/// <c>--rules</c>. Business code is never edited. Plan first; writes only with apply.
/// </summary>
internal static class FixOperation
{
    public static readonly OperationDefinition Definition = new(
        "fix",
        "Fix doctor findings mechanically and without touching source code. Default: missing global.json, .editorconfig, .gitignore lines, .dockerignore. Opt-in via rules: DA-B03 (central package versions, resolved versions unchanged), DA-B07 (warnings are errors in CI/Release), DA-S04 (Domain test project with an architecture test). Plan first; writes only with apply. Other findings are listed as manual.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true),
            new OperationParameter("profile", "Profile file to apply instead of .net-arch/profile.yml."),
            new OperationParameter("rules", "Comma-separated rule ids to fix (default: every default-fixable finding; name opt-in rules here)."),
        },
        Run);

    private static OperationResult Run(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        var report = DoctorRunner.Run(root, new DoctorOptions(request.Get("profile")));
        var only = request.Get("rules")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ctx = new RepoContext(root, null, new NetArch.RuleSettings());
        var plan = new List<FixAction>();
        var manual = new List<string>();

        foreach (var finding in report.Findings.Where(f => f.Severity != DoctorSeverity.Info))
        {
            if (only != null && !only.Contains(finding.Id))
                continue;
            if (StructuralFixers.RuleIds.Contains(finding.Id))
            {
                if (only == null)
                {
                    manual.Add($"{finding.Id}  {finding.Message}  (opt-in: --rules={finding.Id})");
                    continue;
                }

                var structural = StructuralFixers.For(finding.Id, root, ctx, only?.Contains("DA-B03") == true);
                if (structural.Count == 0)
                    manual.Add($"{finding.Id}  {finding.Message}");
                else
                    plan.AddRange(structural);
                continue;
            }

            var fix = Fixer(root, finding);
            if (fix == null)
                manual.Add($"{finding.Id}  {finding.Message}");
            else
                plan.Add(fix);
        }

        foreach (var requested in only ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase))
        {
            if (requested.Equals("DA-S06", StringComparison.OrdinalIgnoreCase) && !plan.Any(p => p.Change.RuleId?.Equals(requested, StringComparison.OrdinalIgnoreCase) == true) && !report.Findings.Any(f => f.Id.Equals(requested, StringComparison.OrdinalIgnoreCase)))
                plan.AddRange(StructuralFixers.For(requested.ToUpperInvariant(), root, ctx, only!.Contains("DA-B03")));
        }

        if (request.Apply)
        {
            foreach (var move in plan.Where(a => a.MoveFrom != null))
            {
                var target = Path.Combine(root, move.Change.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Directory.Move(Path.Combine(root, move.MoveFrom!), target);
            }

            foreach (var action in plan.Where(a => a.MoveFrom == null))
            {
                var path = Path.Combine(root, action.Change.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, action.Content);
            }

            foreach (var action in plan.Where(a => a.AfterWrite != null))
                action.AfterWrite!();
        }

        var lines = plan.Select(p => $"  {p.Change.Action,-7} {p.Change.Path}  [{p.Change.RuleId}] {p.Change.Reason}").ToList();
        var text = $"fix: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {plan.Count} change(s), {manual.Count} manual finding(s)\n"
                   + string.Join("\n", lines)
                   + (manual.Count == 0 ? string.Empty : "\nmanual (not mechanical, or opt-in):\n" + string.Join("\n", manual.Select(m => "  " + m)));
        return new OperationResult(true, text, new { manual }, plan.Select(p => p.Change).ToList(), request.Apply);
    }

    private static FixAction? Fixer(string root, DoctorFinding finding)
    {
        switch (finding.Id)
        {
            case "DA-B01":
                var sdk = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Take(50).Any(f => File.ReadAllText(f).Contains("net9.0", StringComparison.Ordinal)) ? "9.0.100" : "8.0.100";
                return new FixAction(new PlannedChange("global.json", "create", "pin the SDK version", finding.Id),
                        TemplateRenderer.RenderTemplate("V2/solution/global.json.tpl", new Dictionary<string, string> { ["SdkVersion"] = sdk }));
            case "DA-B05":
                return new FixAction(new PlannedChange(".editorconfig", "create", "marker without rules (existing code: no style rules are imposed, so formatting checks keep passing)", finding.Id), TemplateRenderer.Load("V2/ops/editorconfig.adopt.tpl"));
            case "DA-B08":
                return new FixAction(new PlannedChange(".gitignore", "create", "ignore build output, IDE files and secrets", finding.Id), TemplateRenderer.Load("V2/solution/.gitignore.tpl"));
            case "DA-B09":
                var current = File.ReadAllText(Path.Combine(root, ".gitignore"));
                var existing = current.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
                var add = new[] { "bin/", "obj/", ".env" }.Where(l => !existing.Contains(l) && !existing.Contains("**/" + l) && !existing.Contains("/" + l) && !existing.Contains(l.TrimEnd('/'))).ToList();
                return add.Count == 0 ? null : new FixAction(new PlannedChange(".gitignore", "modify", $"append: {string.Join(", ", add)}", finding.Id), current.TrimEnd('\n') + "\n" + string.Join("\n", add) + "\n");
            case "DA-D04":
                return new FixAction(new PlannedChange(".dockerignore", "create", "keep host bin/obj out of the build context", finding.Id), TemplateRenderer.Load("V2/ops/dockerignore.tpl"));
            default:
                return null;
        }
    }
}
