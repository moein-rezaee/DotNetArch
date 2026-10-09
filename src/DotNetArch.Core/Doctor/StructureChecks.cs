namespace DotNetArch.Core.Doctor;

/// <summary>Layers, dependency direction, test projects and layout (DA-S*).</summary>
internal static class StructureChecks
{
    private static readonly (ProjectLayer Layer, string Label)[] RequiredLayers =
    {
        (ProjectLayer.Domain, "Domain"),
        (ProjectLayer.Application, "Application"),
        (ProjectLayer.Infrastructure, "Infrastructure"),
        (ProjectLayer.Api, "Api"),
    };

    // layer -> layers it may reference (itself always allowed); Api/Mcp are composition roots.
    private static readonly Dictionary<ProjectLayer, ProjectLayer[]> Allowed = new()
    {
        [ProjectLayer.Domain] = new[] { ProjectLayer.Contracts },
        [ProjectLayer.Contracts] = Array.Empty<ProjectLayer>(),
        [ProjectLayer.Application] = new[] { ProjectLayer.Domain, ProjectLayer.Contracts },
        [ProjectLayer.Infrastructure] = new[] { ProjectLayer.Application, ProjectLayer.Domain, ProjectLayer.Contracts },
    };

    public static void Run(RepoContext ctx)
    {
        const string cat = "structure";
        var slnFiles = ctx.Files.Where(f => !f.Contains('/') && (f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))).ToList();
        ctx.Check("DA-S01", cat, slnFiles.Count > 0, DoctorSeverity.Warning, "No solution file at the repository root.", hint: "dotnet new sln, then add every project.");

        var missing = RequiredLayers.Where(l => !ctx.OfLayer(l.Layer).Any()).Select(l => l.Label).ToList();
        ctx.Check("DA-S02", cat, missing.Count == 0, DoctorSeverity.Error, $"Missing layer project(s): {string.Join(", ", missing)}.", hint: "Clean Architecture needs <App>.Domain, .Application, .Infrastructure and .Api.");

        var violations = new List<string>();
        foreach (var project in ctx.Projects.Where(p => !p.IsTest && Allowed.ContainsKey(p.Layer)))
        {
            foreach (var reference in project.ProjectReferences)
            {
                var target = ctx.Projects.FirstOrDefault(p => p.Name.Equals(Path.GetFileNameWithoutExtension(reference), StringComparison.OrdinalIgnoreCase));
                if (target == null || target.Layer == ProjectLayer.Other || target.Layer == project.Layer)
                    continue;
                if (!Allowed[project.Layer].Contains(target.Layer))
                    violations.Add($"{project.Name} -> {target.Name}");
            }
        }

        ctx.Check("DA-S03", cat, violations.Count == 0, DoctorSeverity.Error, "Dependencies point outwards (a layer references a layer it must not know).", hint: "Dependencies point inwards: Api/Mcp -> Infrastructure -> Application -> Domain.", details: violations);

        var untested = RequiredLayers.Where(l => ctx.OfLayer(l.Layer).Any() && !ctx.OfLayer(l.Layer, tests: true).Any()).Select(l => l.Label).ToList();
        ctx.Check("DA-S04", cat, untested.Count == 0, DoctorSeverity.Warning, $"No test project for layer(s): {string.Join(", ", untested)}.", hint: "One <App>.<Layer>.Tests per layer.");

        var mcpUntested = ctx.OfLayer(ProjectLayer.Mcp).Any() && !ctx.OfLayer(ProjectLayer.Mcp, tests: true).Any();
        ctx.Check("DA-S05", cat, !mcpUntested, DoctorSeverity.Warning, "MCP host exists without a test project.", hint: "Add <App>.Mcp.Tests (auth, tool catalog, handshake).");

        var layoutOk = ctx.Layout == "v2" || (ctx.Profile?.AcceptedLayouts.Contains(ctx.Layout, StringComparer.OrdinalIgnoreCase) ?? false);
        ctx.Check("DA-S06", cat, layoutOk, DoctorSeverity.Warning, $"Layout is '{ctx.Layout}', the standard is v2 (src/, tests/, kits/).", hint: "A profile can accept other layouts (accepted_layouts); the move to v2 is a separate opt-in step.");

        var unclassified = ctx.Source.Where(p => p.Layer == ProjectLayer.Other && !p.Name.Contains(".Kit.", StringComparison.Ordinal)).Select(p => p.Name).ToList();
        ctx.Check("DA-S07", cat, unclassified.Count == 0, DoctorSeverity.Info, "Projects outside the known layers (Domain/Application/Infrastructure/Api/Mcp/Contracts).", details: unclassified);
    }
}
