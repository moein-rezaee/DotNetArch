using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Doctor;

/// <summary>Diagnoses an existing repository against the DotNetArch standard. Read-only: never writes, never runs a process.</summary>
public static class DoctorRunner
{
    public static DoctorReport Run(string root, DoctorOptions? options = null)
    {
        if (!Directory.Exists(root))
            throw new ArgumentException($"Folder not found: {root}");

        var fullRoot = Path.GetFullPath(root);
        var profile = NetArchStore.ResolveProfile(fullRoot, options?.ProfilePath, out var warning);
        var rules = NetArchStore.LoadEffectiveRules(fullRoot);
        var standards = (NetArchStore.LoadState(fullRoot)?.Standards ?? new List<string>()).Concat(profile?.Standards ?? new List<string>());
        var ctx = new RepoContext(fullRoot, profile, rules, standards);
        StructureChecks.Run(ctx);
        BuildChecks.Run(ctx);
        ConfigChecks.Run(ctx);
        ContainerChecks.Run(ctx);
        DocsChecks.Run(ctx);
        TestChecks.Run(ctx);
        CodeChecks.Run(ctx);
        AbpChecks.Run(ctx);
        ProfileChecks.Run(ctx);

        var accepted = new List<AcceptedFinding>();
        var findings = new List<DoctorFinding>();
        foreach (var original in ctx.Findings)
        {
            var finding = original;
            if (rules.Severity.TryGetValue(finding.Id, out var overridden))
            {
                if (overridden.Equals("off", StringComparison.OrdinalIgnoreCase))
                    continue;
                findings.Add(finding with { Severity = ProfileChecks.ParseSeverity(overridden) });
                continue;
            }

            if (profile != null && profile.Severity.TryGetValue(finding.Id, out var profileSeverity))
            {
                if (profileSeverity.Equals("off", StringComparison.OrdinalIgnoreCase))
                    continue;
                finding = finding with { Severity = ProfileChecks.ParseSeverity(profileSeverity) };
            }

            var exception = rules.Exceptions.FirstOrDefault(e => e.Rule.Equals(finding.Id, StringComparison.OrdinalIgnoreCase) && Covers(e, finding));
            if (exception != null && finding.Severity != DoctorSeverity.Info)
                accepted.Add(new AcceptedFinding(finding.Id, finding.Message, exception.Reason));
            else
                findings.Add(finding);
        }

        findings = findings.OrderByDescending(f => f.Severity).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        var notes = warning == null ? new List<string>() : new List<string> { warning };
        return new DoctorReport(ctx.Root, profile == null ? "none" : $"{profile.Name} {profile.Version}".Trim(), ctx.Layout, ctx.Projects.Select(p => p.Name).ToList(), ctx.Passed, findings, accepted, notes);
    }

    private static bool Covers(RuleException exception, DoctorFinding finding)
    {
        if (string.IsNullOrWhiteSpace(exception.Path))
            return !string.IsNullOrWhiteSpace(exception.Reason);
        var prefix = exception.Path!;
        return !string.IsNullOrWhiteSpace(exception.Reason)
            && (finding.Path?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
                || (finding.Details?.Count > 0 && finding.Details.All(d => d.Contains(prefix, StringComparison.OrdinalIgnoreCase))));
    }
}
