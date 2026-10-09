using DotNetArch.Core.Doctor;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Records the reality of an existing project into <c>.net-arch/</c>. It writes nothing outside that folder and never overwrites rules or the
/// profile reference, so adopting a project cannot change its build, its tests or its behaviour (D-19).
/// </summary>
internal static class AdoptOperation
{
    private const string RulesTemplate = """
        # Rule settings for this project (the project stores only differences from the global ~/.net-arch/rules.yml).
        # severity:   { DA-K04: warning }          # off | info | warning | error
        # thresholds: { coverage_line: 70 }        # minimum line coverage in percent
        # exceptions:                              # accepted deviations; the reason is mandatory
        #   - rule: DA-K04
        #     reason: Why this deviation is accepted
        severity: {}
        thresholds: {}
        exceptions: []

        """;

    public static readonly OperationDefinition Definition = new(
        "adopt",
        "Bring an existing project under tool control without changing any source file: derive layers, kits, entities and modules from the source into .net-arch/project.yml (re-running refreshes the state and keeps rules and profile). Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true),
            new OperationParameter("profile", "Shared profile file to reference from .net-arch/profile.yml (path relative to the repository root or absolute)."),
            new OperationParameter("standards", "Comma-separated standards to record in project.yml (for example abp).", Choices: new[] { "abp" }),
        },
        Run);

    private static OperationResult Run(OperationRequest request)
    {
        var root = Path.GetFullPath(request.Get("path") ?? Directory.GetCurrentDirectory());
        if (!Directory.Exists(root))
            throw new ArgumentException($"Folder not found: {root}");

        var folder = NetArchStore.ProjectFolder(root);
        var ctx = new RepoContext(root, null, new RuleSettings());
        var state = StateBuilder.Build(ctx, NetArchStore.LoadState(root));
        foreach (var standard in (request.Get("standards") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!state.Standards.Contains(standard, StringComparer.OrdinalIgnoreCase))
                state.Standards.Add(standard.ToLowerInvariant());
        }

        var files = new List<(string Relative, string Content, bool Overwrite, string Reason)>
        {
            ($"{NetArchFiles.Folder}/{NetArchFiles.State}", NetArchStore.ToYaml(state), true, "state derived from the source"),
            ($"{NetArchFiles.Folder}/{NetArchFiles.Rules}", RulesTemplate, false, "rule settings (created once, never overwritten)"),
            ($"{NetArchFiles.Folder}/{NetArchFiles.Lock}", NetArchStore.ToYaml(new GeneratedLock()), false, "fingerprints of generated files (empty: nothing was generated)"),
        };

        if (request.Get("profile") is { } profilePath)
        {
            var fullProfile = Path.GetFullPath(Path.IsPathRooted(profilePath) ? profilePath : Path.Combine(root, profilePath));
            var shared = File.Exists(fullProfile) ? NetArchStore.LoadProfileFile(fullProfile) : null;
            if (shared == null)
                throw new ArgumentException($"Profile file not found or empty: {profilePath}");
            var reference = new ProfileDefinition { Name = shared.Name, Version = shared.Version, Source = Path.GetRelativePath(root, fullProfile).Replace('\\', '/') };
            files.Add(($"{NetArchFiles.Folder}/{NetArchFiles.Profile}", NetArchStore.ToYaml(reference), false, "reference to the shared profile (created once)"));
        }

        var plan = new List<PlannedChange>();
        foreach (var (relative, content, overwrite, reason) in files)
        {
            var path = Path.Combine(root, relative);
            if (!relative.StartsWith(NetArchFiles.Folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("adopt may only write inside .net-arch/");
            if (!File.Exists(path))
                plan.Add(new PlannedChange(relative, "create", reason));
            else if (overwrite && File.ReadAllText(path) != content)
                plan.Add(new PlannedChange(relative, "modify", reason));
        }

        if (request.Apply)
        {
            Directory.CreateDirectory(folder);
            foreach (var change in plan)
            {
                var content = files.First(f => f.Relative == change.Path).Content;
                File.WriteAllText(Path.Combine(root, change.Path), content);
            }
        }

        var text = plan.Count == 0
            ? $"adopt: {root} is already adopted and up to date (0 changes)."
            : $"adopt: {(request.Apply ? "applied" : "plan (dry run, add --apply to write)")} - {plan.Count} change(s), all inside .net-arch/\n" + string.Join("\n", plan.Select(c => $"  {c.Action,-7} {c.Path}  ({c.Reason})"));
        return new OperationResult(true, text, state, plan, request.Apply);
    }
}
