using System.Text.RegularExpressions;

namespace DotNetArch.Core.Doctor;

/// <summary>Source rules from AGENTS.md: no console diagnostics, no sync-over-async, no IQueryable ports, no hard-coded secrets, kit boundaries (DA-K*).</summary>
internal static partial class CodeChecks
{
    public static void Run(RepoContext ctx)
    {
        const string cat = "code";
        var sources = ctx.Files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !IsTestOrGenerated(f)).ToList();
        var console = new List<string>();
        var syncOverAsync = new List<string>();
        var queryable = new List<string>();
        var anyOrigin = new List<string>();
        var secrets = new List<string>();

        foreach (var file in sources)
        {
            var text = ctx.Read(file);
            if (text.Length == 0)
                continue;
            if (ConsoleCall().IsMatch(text) && !file.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase))
                console.Add(file);
            if (SyncOverAsync().IsMatch(text))
                syncOverAsync.Add(file);
            if (AnyOrigin().IsMatch(text))
                anyOrigin.Add(file);
            if (HardCodedSecret().IsMatch(text))
                secrets.Add(file);
            var project = ctx.Projects.Where(p => file.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => p.Dir.Length).FirstOrDefault();
            if (project is { Layer: ProjectLayer.Domain or ProjectLayer.Application } && QueryableUse().IsMatch(Operations.LayerMigration.Strip(text)))
                queryable.Add(file);
        }

        ctx.Check("DA-K01", cat, console.Count == 0, DoctorSeverity.Warning, "Console.Write* used for diagnostics.", hint: "Use ILogger.", details: console);
        ctx.Check("DA-K02", cat, syncOverAsync.Count == 0, DoctorSeverity.Warning, "Sync-over-async (.Result / .Wait() / GetAwaiter().GetResult()).", hint: "await the call.", details: syncOverAsync);
        ctx.Check("DA-K03", cat, queryable.Count == 0, DoctorSeverity.Warning, "IQueryable exposed from Domain/Application.", hint: "Repositories return materialised results via async methods.", details: queryable);
        ctx.Check("DA-K04", cat, anyOrigin.Count == 0, DoctorSeverity.Warning, "CORS AllowAnyOrigin.", hint: "Configure explicit origins.", details: anyOrigin);
        ctx.Check("DA-K05", cat, secrets.Count == 0, DoctorSeverity.Warning, "Possible hard-coded secret literal in source.", hint: "Read it from configuration / Vault.", details: secrets);

        var boundary = ctx.Source
            .Where(p => p.Layer is ProjectLayer.Domain or ProjectLayer.Application)
            .SelectMany(p => p.Packages.Where(x => KitImplementation().IsMatch(x.Name)).Select(x => $"{p.Name}: {x.Name}"))
            .ToList();
        ctx.Check("DA-K06", cat, boundary.Count == 0, DoctorSeverity.Error, "Domain/Application reference a kit Core/Provider package (only Abstractions are allowed there).", hint: "Core + Providers belong in the composition root only.", details: boundary);

        var kits = ctx.Projects.SelectMany(p => p.Packages).Select(x => x.Name).Where(n => n.Contains(".Kit.", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (kits.Count > 0)
            ctx.Note("DA-K07", cat, $"Kit packages in use ({kits.Count}): {string.Join(", ", kits)}");
    }

    private static bool IsTestOrGenerated(string file) =>
        file.Split('/').Any(s => s.EndsWith(".Tests", StringComparison.Ordinal) || s.Equals("tests", StringComparison.OrdinalIgnoreCase) || s.Equals("test", StringComparison.OrdinalIgnoreCase) || s.Equals("Migrations", StringComparison.OrdinalIgnoreCase))
        || file.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\bConsole\.(Write|WriteLine)\(")]
    private static partial Regex ConsoleCall();

    [GeneratedRegex(@"\.GetAwaiter\(\)\s*\.GetResult\(\)|Async\([^;\n]*\)\.Result\b|\.Wait\(\)")]
    private static partial Regex SyncOverAsync();

    [GeneratedRegex(@"\bIQueryable<")]
    private static partial Regex QueryableUse();

    [GeneratedRegex(@"AllowAnyOrigin\(\)")]
    private static partial Regex AnyOrigin();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|secret|apikey|api_key|clientsecret|accesskey|secretkey)\w*\s*=\s*""[^""{}$\s][^""]{5,}""")]
    private static partial Regex HardCodedSecret();

    [GeneratedRegex(@"\.Kit\.[A-Za-z]+\.(Core|Providers)")]
    private static partial Regex KitImplementation();
}
