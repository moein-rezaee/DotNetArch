using System.Text.RegularExpressions;

namespace DotNetArch.Core.Doctor;

/// <summary>
/// The built-in, opt-in <c>abp</c> rule set (DA-A*): structure and naming rules taken from the ABP Framework solution conventions
/// (docs/specs/abp-alignment.md). It is switched on by <c>standards: [abp]</c> in project.yml or in a profile, is off by default and has no
/// rule about ids, application services or the ABP runtime framework.
/// </summary>
internal static partial class AbpChecks
{
    public const string Standard = "abp";

    private static readonly (ProjectLayer Layer, string Suffix)[] LayerProjects =
    {
        (ProjectLayer.DomainShared, "Domain.Shared"),
        (ProjectLayer.ApplicationContracts, "Application.Contracts"),
        (ProjectLayer.HttpApi, "HttpApi"),
        (ProjectLayer.HttpApiClient, "HttpApi.Client"),
    };

    // layer -> the only source layers it may reference (ABP package rules)
    private static readonly Dictionary<ProjectLayer, ProjectLayer[]> Direction = new()
    {
        [ProjectLayer.DomainShared] = Array.Empty<ProjectLayer>(),
        [ProjectLayer.Domain] = new[] { ProjectLayer.DomainShared },
        [ProjectLayer.ApplicationContracts] = new[] { ProjectLayer.DomainShared },
        [ProjectLayer.HttpApi] = new[] { ProjectLayer.ApplicationContracts, ProjectLayer.DomainShared },
        [ProjectLayer.HttpApiClient] = new[] { ProjectLayer.ApplicationContracts, ProjectLayer.DomainShared },
    };

    public static void Run(RepoContext ctx)
    {
        if (!ctx.Standards.Contains(Standard))
            return;

        const string cat = "abp";
        ctx.Check("DA-A01", cat, ctx.Layout == "v3", DoctorSeverity.Warning, $"Layout is '{ctx.Layout}', the ABP layout is v3 (src/, test/, optional etc/).", hint: "dotnet-arch fix --rules=DA-A01 moves tests/ to test/ (flat layouts: DA-S06 first).");

        var analysis = Operations.LayerMigration.Analyze(ctx);
        var missing = Operations.LayerMigration.Targets
            .Where(l => analysis.Moves.TryGetValue(l, out var moves) && moves.Count > 0 && !ctx.OfLayer(l).Any())
            .Select(l => $"{Operations.LayerMigration.Suffix(l)} ({analysis.Moves[l].Count} file(s) belong there)")
            .ToList();
        ctx.Check("DA-A02", cat, missing.Count == 0, DoctorSeverity.Warning, $"ABP layer project(s) missing where files belong in them: {string.Join(", ", missing)}.", hint: "dotnet-arch fix --rules=DA-A02 creates them and moves the files (namespaces unchanged); what cannot move is listed as manual.", details: analysis.Moves.Where(m => !ctx.OfLayer(m.Key).Any()).SelectMany(m => m.Value.Select(x => x.From)).ToList());

        var empty = LayerProjects
            .SelectMany(l => ctx.OfLayer(l.Layer).Where(p => !ctx.Files.Any(f => f.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))))
            .Select(p => p.Name)
            .ToList();
        ctx.Check("DA-A07", cat, empty.Count == 0, DoctorSeverity.Warning, "ABP layer project(s) without any source file: a layer exists only where there is something for it.", hint: "Move the matching files in, develop the layer, or remove the project.", details: empty);

        ctx.Check("DA-A08", cat, !Operations.TypedClient.IsMissing(ctx), DoctorSeverity.Warning, "The API has controllers but no typed client (HttpApi.Client) for the services that call it.", hint: "dotnet-arch fix --rules=DA-A08 generates it from the controller routes; actions it cannot express are listed as manual.");

        var compose = Operations.EtcMove.Pending(ctx);
        ctx.Check("DA-A09", cat, compose.Count == 0, DoctorSeverity.Warning, "Docker Compose file(s) at the repository root belong in etc/docker/.", hint: "dotnet-arch fix --rules=DA-A09 moves them, re-bases their relative paths and updates mentions.", details: compose.ToList());

        var violations = new List<string>();
        foreach (var project in ctx.Source.Where(p => Direction.ContainsKey(p.Layer)))
        {
            foreach (var reference in project.ProjectReferences)
            {
                var target = ctx.Projects.FirstOrDefault(p => p.Name.Equals(Path.GetFileNameWithoutExtension(reference), StringComparison.OrdinalIgnoreCase));
                if (target == null || target.Layer == ProjectLayer.Other || target.Layer == project.Layer)
                    continue;
                if (!Direction[project.Layer].Contains(target.Layer))
                    violations.Add($"{project.Name} -> {target.Name}");
            }
        }

        ctx.Check("DA-A03", cat, violations.Count == 0, DoctorSeverity.Warning, "ABP reference direction broken (Domain.Shared: none; Domain: Domain.Shared; Contracts: Domain.Shared; HttpApi and HttpApi.Client: Contracts only).", hint: "Remove the reference or move the shared type to the right layer.", details: violations);

        var repositoryIssues = new List<string>();
        var domainServices = new List<string>();
        var contractsServices = new List<string>();
        var dtoInApplication = new List<string>();
        var hasContracts = ctx.OfLayer(ProjectLayer.ApplicationContracts).Any();
        foreach (var file in ctx.Files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !IsTestOrGenerated(f)))
        {
            var project = ctx.Projects.Where(p => !p.IsTest && file.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => p.Dir.Length).FirstOrDefault();
            if (project == null)
                continue;
            var text = ctx.Read(file);
            if (text.Length == 0)
                continue;
            if (project.Layer is ProjectLayer.Domain or ProjectLayer.Application or ProjectLayer.DomainShared or ProjectLayer.ApplicationContracts)
                repositoryIssues.AddRange(RepositoryIssues(file, text));
            if (project.Layer == ProjectLayer.Domain)
                domainServices.AddRange(DomainService().Matches(text).Select(m => $"{file}: {m.Groups["name"].Value}"));
            if (project.Layer == ProjectLayer.ApplicationContracts)
                contractsServices.AddRange(AppServiceInterface().Matches(text).Where(m => !m.Groups["name"].Value.EndsWith("AppService", StringComparison.Ordinal)).Select(m => $"{file}: {m.Groups["name"].Value}"));
            if (hasContracts && project.Layer == ProjectLayer.Application)
                dtoInApplication.AddRange(DtoType().Matches(text).Select(m => $"{file}: {m.Groups["name"].Value}"));
        }

        ctx.Check("DA-A04", cat, repositoryIssues.Count == 0, DoctorSeverity.Warning, "Repository interfaces must have async methods, an optional CancellationToken as the last parameter and no IQueryable result.", hint: "Task<T> Method(..., CancellationToken cancellationToken = default).", details: repositoryIssues);
        ctx.Check("DA-A05", cat, domainServices.Count == 0 && contractsServices.Count == 0, DoctorSeverity.Warning, "Naming: domain services end with 'Manager'; application service interfaces end with 'AppService'.", hint: "Rename the type.", details: domainServices.Concat(contractsServices).ToList());
        ctx.Check("DA-A06", cat, dtoInApplication.Count == 0, DoctorSeverity.Warning, "DTO types live in Application.Contracts, not in Application.", hint: "Move the file to the Contracts project and keep the namespace (a reviewed migration step).", details: dtoInApplication);
    }

    private static IEnumerable<string> RepositoryIssues(string file, string text)
    {
        foreach (Match type in RepositoryInterface().Matches(text))
        {
            var name = type.Groups["name"].Value;
            foreach (Match method in Method().Matches(type.Groups["body"].Value))
            {
                var returns = method.Groups["ret"].Value.Trim();
                var parameters = SplitParameters(method.Groups["args"].Value);
                var problems = new List<string>();
                if (!returns.StartsWith("Task", StringComparison.Ordinal) && !returns.StartsWith("ValueTask", StringComparison.Ordinal))
                    problems.Add("not async");
                if (returns.Contains("IQueryable", StringComparison.Ordinal))
                    problems.Add("returns IQueryable");
                if (parameters.Count == 0 || !parameters[^1].Contains("CancellationToken", StringComparison.Ordinal))
                    problems.Add("no trailing CancellationToken");
                if (problems.Count > 0)
                    yield return $"{file}: {name}.{method.Groups["name"].Value} ({string.Join(", ", problems)})";
            }
        }
    }

    private static List<string> SplitParameters(string arguments)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();
        foreach (var c in arguments)
        {
            if (c is '<' or '(' or '[')
                depth++;
            else if (c is '>' or ')' or ']')
                depth--;
            if (c == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.ToString().Trim().Length > 0)
            parts.Add(current.ToString().Trim());
        return parts;
    }

    private static bool IsTestOrGenerated(string file) =>
        file.Split('/').Any(s => s.EndsWith(".Tests", StringComparison.Ordinal) || s.Equals("tests", StringComparison.OrdinalIgnoreCase) || s.Equals("test", StringComparison.OrdinalIgnoreCase) || s.Equals("Migrations", StringComparison.OrdinalIgnoreCase))
        || file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\binterface\s+(?<name>I\w*Repository)\b[^{]*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RepositoryInterface();

    [GeneratedRegex(@"^[ \t]*(?<ret>[A-Za-z_][\w<>\[\]?,. ]*?)[ \t]+(?<name>[A-Za-z_]\w*)[ \t]*\((?<args>[^)]*)\)[ \t]*(?:where [^;]*)?;", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Method();

    [GeneratedRegex(@"\b(?:class|record)\s+(?<name>\w+Service)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DomainService();

    [GeneratedRegex(@"\binterface\s+(?<name>I\w*Service)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AppServiceInterface();

    [GeneratedRegex(@"\b(?:class|record|struct)\s+(?<name>\w+Dto)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DtoType();
}
