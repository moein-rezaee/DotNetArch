namespace DotNetArch.Core.Doctor;

/// <summary>README, AGENTS and bilingual documentation pairs (DA-M*).</summary>
internal static class DocsChecks
{
    public static void Run(RepoContext ctx)
    {
        const string cat = "docs";
        ctx.Check("DA-M01", cat, ctx.Has("README.md"), DoctorSeverity.Warning, "README.md is missing.");
        ctx.Check("DA-M02", cat, ctx.Has("README.fa.md"), DoctorSeverity.Warning, "README.fa.md (Persian pair) is missing.");
        ctx.Check("DA-M03", cat, ctx.Has("AGENTS.md"), DoctorSeverity.Warning, "AGENTS.md (rules for agents) is missing.");
        ctx.Check("DA-M04", cat, ctx.DirectoryExists("docs"), DoctorSeverity.Warning, "docs/ folder is missing (specs, roadmap, decisions).");

        var docs = ctx.Files.Where(f => f.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList();
        var fileSet = docs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unpaired = new List<string>();
        foreach (var doc in docs)
        {
            if (doc.EndsWith(".fa.md", StringComparison.OrdinalIgnoreCase))
            {
                if (!fileSet.Contains(doc[..^6] + ".md"))
                    unpaired.Add(doc);
            }
            else if (!fileSet.Contains(doc[..^3] + ".fa.md"))
            {
                unpaired.Add(doc);
            }
        }

        ctx.Check("DA-M05", cat, unpaired.Count == 0, DoctorSeverity.Warning, "docs/ files without their English/Persian counterpart.", hint: "Keep .md and .fa.md pairs in sync.", details: unpaired);
    }
}
