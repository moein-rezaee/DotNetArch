using YamlDotNet.Serialization;

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

        ctx.Check("DA-M06", cat, ctx.Has("docs/INDEX.md") || ctx.Has("docs/README.md"), DoctorSeverity.Warning, "docs/INDEX.md (documentation index) is missing.", hint: "List every document and where to start.");

        var requiredSpecs = new[] { "overview", "contracts", "acceptance", "changelog" };
        var missingSpecs = requiredSpecs.Where(n => !ctx.Has($"docs/specs/{n}.md")).Select(n => $"docs/specs/{n}.md").ToList();
        ctx.Check("DA-M07", cat, missingSpecs.Count == 0, DoctorSeverity.Warning, "Spec documents are missing.", hint: "overview, contracts, acceptance and changelog describe behaviour; change them with the code.", details: missingSpecs);

        var broken = new List<string>();
        foreach (var spec in new[] { "docs/specs/openspec.yaml", "docs/specs/testspec.yaml" }.Where(ctx.Has))
        {
            try
            {
                new DeserializerBuilder().Build().Deserialize<object>(ctx.Read(spec));
            }
            catch (YamlDotNet.Core.YamlException)
            {
                broken.Add(spec);
            }
        }

        ctx.Check("DA-M08", cat, broken.Count == 0, DoctorSeverity.Error, "Machine-readable spec files do not parse as YAML.", details: broken);

        var exposesApi = ctx.OfLayer(ProjectLayer.Api).Any(p => p.Packages.Any(x => x.Name.Contains("Swagger", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Swashbuckle", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("OpenApi", StringComparison.OrdinalIgnoreCase)));
        if (exposesApi)
            ctx.Check("DA-M09", cat, ctx.Has("docs/api/openapi.json") || ctx.Has("docs/api/openapi.yaml"), DoctorSeverity.Warning, "The API exposes Swagger but no OpenAPI snapshot is committed under docs/api/.", hint: "Export openapi.json from the running API and review it with the code.");

        ctx.Check("DA-M05", cat, unpaired.Count == 0, DoctorSeverity.Warning, "docs/ files without their English/Persian counterpart.", hint: "Keep .md and .fa.md pairs in sync.", details: unpaired);
    }
}
