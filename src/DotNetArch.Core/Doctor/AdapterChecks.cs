using System.Text.RegularExpressions;

namespace DotNetArch.Core.Doctor;

/// <summary>
/// The built-in, opt-in <c>adapters</c> rule set (DA-AD*): a module is ready to be composed when each adapter (a project <c>&lt;App&gt;.Infrastructure.&lt;System&gt;</c>)
/// depends on the module core only, registers itself with one extension method and is a package of its own, and when nothing but a composition project
/// picks the adapters. It is switched on by <c>standards: [adapters]</c> in project.yml or in a profile and names nothing that belongs to an organisation
/// (docs/specs/adapters.md, decision D-40).
/// </summary>
internal static partial class AdapterChecks
{
    public const string Standard = "adapters";
    private const string Marker = ".Infrastructure.";

    /// <summary>The name of the external system of an adapter project (<c>SepidarRest</c> in <c>OrderService.Infrastructure.SepidarRest</c>), or null when it is not one.</summary>
    public static string? SystemOf(ProjectInfo project)
    {
        if (project.IsTest || project.Layer != ProjectLayer.Infrastructure)
            return null;
        var index = project.Name.IndexOf(Marker, StringComparison.Ordinal);
        return index < 0 ? null : project.Name[(index + Marker.Length)..];
    }

    public static IEnumerable<ProjectInfo> Adapters(RepoContext ctx) => ctx.Source.Where(p => SystemOf(p) != null);

    /// <summary>The shared Infrastructure project of the module (<c>&lt;App&gt;.Infrastructure</c>), if there is one.</summary>
    public static ProjectInfo? Host(RepoContext ctx) =>
        ctx.OfLayer(ProjectLayer.Infrastructure).FirstOrDefault(p => p.Name.EndsWith(".Infrastructure", StringComparison.Ordinal));

    public static ProjectInfo? Target(RepoContext ctx, string reference) =>
        ctx.Projects.FirstOrDefault(p => p.Name.Equals(Path.GetFileNameWithoutExtension(reference), StringComparison.OrdinalIgnoreCase));

    /// <summary>True when a source file of <paramref name="project"/> names a namespace of <paramref name="target"/> that is not its own.</summary>
    public static bool Uses(RepoContext ctx, ProjectInfo project, ProjectInfo target)
    {
        var pattern = new Regex(@"(?<![\w.])" + Regex.Escape(target.Name) + @"(?:\.[A-Za-z_]\w*)*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        foreach (var file in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (Match match in pattern.Matches(ctx.Read(file)))
            {
                // a name inside the project's own namespace tree is not a use, unless the target itself sits below the project (host -> adapter)
                var name = match.Value;
                var own = name.Equals(project.Name, StringComparison.Ordinal) || name.StartsWith(project.Name + ".", StringComparison.Ordinal);
                var targetBelow = target.Name.StartsWith(project.Name + ".", StringComparison.Ordinal);
                if (!own || targetBelow)
                    return true;
            }
        }

        return false;
    }

    public static void Run(RepoContext ctx)
    {
        if (!ctx.Standards.Contains(Standard))
            return;

        const string cat = "adapters";
        var adapters = Adapters(ctx).ToList();
        var host = Host(ctx);

        var forbidden = new List<string>();
        var between = new List<string>();
        foreach (var adapter in adapters)
        {
            foreach (var reference in adapter.ProjectReferences)
            {
                var target = Target(ctx, reference);
                if (target == null)
                    continue;
                if (SystemOf(target) != null)
                    between.Add($"{adapter.Name} -> {target.Name}");
                else if (target.Layer is ProjectLayer.Application or ProjectLayer.Infrastructure or ProjectLayer.Api or ProjectLayer.Mcp or ProjectLayer.Cli or ProjectLayer.HttpApi or ProjectLayer.HttpApiClient)
                    forbidden.Add($"{adapter.Name} -> {target.Name}");
            }
        }

        ctx.Check("DA-AD01", cat, forbidden.Count == 0, DoctorSeverity.Warning, "An adapter references more than the module core (it must depend on Domain, Domain.Shared, Contracts and Application.Contracts only).",
            hint: "fix --rules=DA-AD01 removes a reference the adapter does not use; a used one means the type belongs in the core: move_code it there.", details: forbidden);

        var hosted = host == null ? new List<string>() : host.ProjectReferences.Select(r => Target(ctx, r)).Where(t => t != null && SystemOf(t) != null).Select(t => $"{host.Name} -> {t!.Name}").ToList();
        ctx.Check("DA-AD02", cat, hosted.Count == 0, DoctorSeverity.Warning, "The shared Infrastructure project references adapter projects; only a composition project (or a host) may pick adapters.",
            hint: "move_code the adapter-specific code into the adapter, add_composition, then fix --rules=DA-AD02 removes the references.", details: hosted);

        ctx.Check("DA-AD03", cat, between.Count == 0, DoctorSeverity.Warning, "An adapter references another adapter.", hint: "Shared code belongs in the module core (move_code) or, when several modules need it, in a Kit.", details: between);

        var withoutRegistration = adapters.Where(a => !ctx.Files.Any(f => f.StartsWith(a.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && Registration().IsMatch(ctx.Read(f)))).Select(a => a.Name).ToList();
        ctx.Check("DA-AD04", cat, withoutRegistration.Count == 0, DoctorSeverity.Warning, "An adapter has no registration extension method (public static IServiceCollection Add...(this IServiceCollection ...)).",
            hint: "One method registers everything the adapter owns, so a composition can add it.", details: withoutRegistration);

        var unpackaged = adapters.Where(a => !ctx.Read(a.File).Contains("<PackageId>", StringComparison.Ordinal)).Select(a => a.Name).ToList();
        ctx.Check("DA-AD05", cat, unpackaged.Count == 0, DoctorSeverity.Warning, "An adapter is not a package of its own (no PackageId).",
            hint: "The profile's adapter_package pattern names it; fix --rules=DA-AD05 writes PackageId, IsPackable and Description.", details: unpackaged);
    }

    [GeneratedRegex(@"public\s+static\s+IServiceCollection\s+Add\w+\s*\(\s*this\s+IServiceCollection", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Registration();
}
