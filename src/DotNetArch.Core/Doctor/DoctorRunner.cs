namespace DotNetArch.Core.Doctor;

/// <summary>Diagnoses an existing repository against the DotNetArch standard. Read-only: never writes, never runs a process.</summary>
public static class DoctorRunner
{
    public static DoctorReport Run(string root, DoctorOptions? options = null)
    {
        if (!Directory.Exists(root))
            throw new ArgumentException($"Folder not found: {root}");

        var ctx = new RepoContext(root, (options ?? new DoctorOptions()).Profile);
        StructureChecks.Run(ctx);
        BuildChecks.Run(ctx);
        ConfigChecks.Run(ctx);
        ContainerChecks.Run(ctx);
        DocsChecks.Run(ctx);
        CodeChecks.Run(ctx);
        CoreviaChecks.Run(ctx);

        var findings = ctx.Findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ToList();
        return new DoctorReport(ctx.Root, ctx.Profile.ToString().ToLowerInvariant(), ctx.Layout, ctx.Projects.Select(p => p.Name).ToList(), ctx.Passed, findings);
    }
}
