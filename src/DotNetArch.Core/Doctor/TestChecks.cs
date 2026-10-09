using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DotNetArch.Core.Doctor;

/// <summary>Do the tests exist, and how much do they cover (DA-T*)? Coverage is read from Cobertura reports; the tool never runs the tests.</summary>
internal static partial class TestChecks
{
    public const string CoverageThresholdKey = "coverage_line";

    public static void Run(RepoContext ctx)
    {
        const string cat = "tests";
        var testProjects = ctx.Projects.Where(p => p.IsTest).ToList();
        if (testProjects.Count == 0)
        {
            ctx.Note("DA-T00", cat, "No test projects found; test checks skipped.");
            return;
        }

        var empty = testProjects
            .Where(p => !ctx.Files.Any(f => f.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && TestAttribute().IsMatch(ctx.Read(f))))
            .Select(p => p.Name)
            .ToList();
        ctx.Check("DA-T01", cat, empty.Count == 0, DoctorSeverity.Warning, "Test projects without a single test.", hint: "Add tests or remove the project.", details: empty);

        var reports = FindCoverageReports(ctx.Root);
        if (reports.Count == 0)
        {
            ctx.Note("DA-T02", cat, "No coverage report found.", "dotnet test --collect:\"XPlat Code Coverage\" (Cobertura) and run doctor again.");
            return;
        }

        var rates = reports.Select(ReadLineRate).Where(r => r.HasValue).Select(r => r!.Value).ToList();
        if (rates.Count == 0)
        {
            ctx.Note("DA-T02", cat, "Coverage report found but no line-rate could be read.");
            return;
        }

        var percent = rates.Average() * 100;
        var threshold = ctx.Rules.Thresholds.TryGetValue(CoverageThresholdKey, out var t) ? t : 0;
        var text = percent.ToString("0.0", CultureInfo.InvariantCulture);
        if (percent + 0.0001 >= threshold)
            ctx.Note("DA-T02", cat, $"Line coverage {text}% (threshold {threshold.ToString("0", CultureInfo.InvariantCulture)}%).");
        else
            ctx.Check("DA-T02", cat, false, DoctorSeverity.Warning, $"Line coverage {text}% is below the threshold {threshold.ToString("0", CultureInfo.InvariantCulture)}%.", hint: "Add tests or change thresholds.coverage_line in rules.yml.");
    }

    private static List<string> FindCoverageReports(string root)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
        try
        {
            return Directory.EnumerateFiles(root, "coverage.cobertura.xml", options)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToList();
        }
        catch (IOException)
        {
            return new List<string>();
        }
    }

    private static double? ReadLineRate(string path)
    {
        try
        {
            var value = XDocument.Load(path).Root?.Attribute("line-rate")?.Value;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) ? rate : null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\[\s*(Fact|Theory|Test|TestMethod|TestCase)\b")]
    private static partial Regex TestAttribute();
}
