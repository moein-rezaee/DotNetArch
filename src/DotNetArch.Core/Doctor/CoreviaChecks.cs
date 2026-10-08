namespace DotNetArch.Core.Doctor;

/// <summary>Corevia governance rules, only under the corevia profile (DA-V*).</summary>
internal static class CoreviaChecks
{
    private static readonly string[] RequiredMetadata =
    {
        ".corevia/repo.yaml", ".corevia/follows.yaml", ".corevia/validators.yaml", ".corevia/skills.yaml",
    };

    private static readonly string[] McpGovernanceFiles =
    {
        "McpApprovalVerifier", "McpAudit", "McpAuthentication", "McpSignedEnvelope",
    };

    public static void Run(RepoContext ctx)
    {
        if (!ctx.IsCorevia)
            return;

        const string cat = "corevia";
        var missing = RequiredMetadata.Where(f => !ctx.Has(f)).ToList();
        ctx.Check("DA-V01", cat, missing.Count == 0, DoctorSeverity.Error, "Governance metadata missing.", hint: "Run corevia-standards service-repo.provision.", details: missing);

        var folder = Path.GetFileName(ctx.Root.TrimEnd(Path.DirectorySeparatorChar));
        ctx.Check("DA-V02", cat, folder.StartsWith("corevia-", StringComparison.Ordinal), DoctorSeverity.Warning, $"Repository folder '{folder}' does not follow corevia-<slug>.", hint: "GitLab path = clone folder = corevia-<slug>.");

        var shared = ctx.Projects.SelectMany(p => p.ProjectReferences.Where(r => r.Replace('\\', '/').Contains("/shared/", StringComparison.OrdinalIgnoreCase) || r.StartsWith("shared/", StringComparison.OrdinalIgnoreCase)).Select(r => $"{p.Name} -> {r}")).ToList();
        ctx.Check("DA-V03", cat, shared.Count == 0, DoctorSeverity.Error, "Projects still reference the monorepo shared/* sources.", hint: "Use Corevia.Kit.* packages from the Nexus feed.", details: shared);

        ctx.Check("DA-V04", cat, ctx.Has("NuGet.config"), DoctorSeverity.Warning, "NuGet.config (Nexus feed) is missing.");
        ctx.Check("DA-V05", cat, ctx.Has(".gitlab-ci.yml") && ctx.Files.Any(f => f.StartsWith(".gitlab/ci/", StringComparison.OrdinalIgnoreCase)), DoctorSeverity.Warning, "CI is not split (.gitlab-ci.yml + .gitlab/ci/*).", hint: "corevia-standards service-cicd.split");
        ctx.Check("DA-V06", cat, ctx.Has("docs/evidence/docker-build-proof.yaml"), DoctorSeverity.Warning, "No docker build proof (docs/evidence/docker-build-proof.yaml).");

        var mcp = ctx.OfLayer(ProjectLayer.Mcp).FirstOrDefault();
        if (mcp != null)
        {
            var local = McpGovernanceFiles.Where(n => ctx.Files.Any(f => f.StartsWith(mcp.Dir + "/", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(f).Contains(n, StringComparison.Ordinal))).ToList();
            ctx.Check("DA-V07", cat, local.Count == 0, DoctorSeverity.Info, $"MCP governance code is copied into the service ({string.Join(", ", local)}); candidate for a shared Mcp kit.", hint: "Planned Corevia MCP kit (signed envelope, approval, audit).");
        }
    }
}
