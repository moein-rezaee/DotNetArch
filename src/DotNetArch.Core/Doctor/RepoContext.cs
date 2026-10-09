using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Doctor;

internal enum ProjectLayer
{
    Other,
    Domain,
    Application,
    Infrastructure,
    Api,
    Mcp,
    Contracts,
}

internal sealed record ProjectInfo(
    string Name,
    string Dir,
    string File,
    ProjectLayer Layer,
    bool IsTest,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageReferenceInfo> Packages,
    bool HasNullable,
    bool HasTreatWarningsAsErrors);

internal sealed record PackageReferenceInfo(string Name, bool InlineVersion);

/// <summary>Read-only view of the repository under diagnosis: file list, parsed projects, and the pass/finding recorder.</summary>
internal sealed class RepoContext
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", ".vs", ".idea", ".work", "TestResults", ".terraform", ".venv",
    };

    private readonly List<DoctorFinding> _findings = new();
    private readonly List<string> _passed = new();

    public RepoContext(string root, ProfileDefinition? profile, RuleSettings rules)
    {
        Root = Path.GetFullPath(root);
        Files = Enumerate(Root).ToList();
        Projects = Files.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Select(ParseProject).ToList();
        Profile = profile;
        Rules = rules;
        Layout = DetectLayout();
    }

    public string Root { get; }

    public ProfileDefinition? Profile { get; }

    public RuleSettings Rules { get; }

    public string Layout { get; }

    public IReadOnlyList<string> Files { get; }

    public IReadOnlyList<ProjectInfo> Projects { get; }

    public IReadOnlyList<DoctorFinding> Findings => _findings;

    public IReadOnlyList<string> Passed => _passed;

    public IEnumerable<ProjectInfo> Source => Projects.Where(p => !p.IsTest);

    public bool Has(string relative) => Files.Contains(relative, StringComparer.OrdinalIgnoreCase);

    public bool HasFile(string fileName) => Files.Any(f => System.IO.Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

    public bool DirectoryExists(string relative) => Directory.Exists(System.IO.Path.Combine(Root, relative));

    public string Read(string relative, int maxBytes = 512 * 1024)
    {
        var path = System.IO.Path.Combine(Root, relative);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maxBytes)
            return string.Empty;
        return File.ReadAllText(path);
    }

    public IEnumerable<ProjectInfo> OfLayer(ProjectLayer layer, bool tests = false) =>
        Projects.Where(p => p.Layer == layer && p.IsTest == tests);

    /// <summary>Records one check: a pass when <paramref name="ok"/>, otherwise a finding.</summary>
    public void Check(string id, string category, bool ok, DoctorSeverity severity, string message, string? path = null, string? hint = null, IReadOnlyList<string>? details = null)
    {
        if (ok)
        {
            _passed.Add(id);
            return;
        }

        _findings.Add(new DoctorFinding(id, severity, category, message, path, hint, details is { Count: > 20 } ? details.Take(20).Append($"... and {details.Count - 20} more").ToList() : details));
    }

    public void Note(string id, string category, string message, string? hint = null) =>
        _findings.Add(new DoctorFinding(id, DoctorSeverity.Info, category, message, null, hint));

    private string DetectLayout()
    {
        if (Projects.Any(p => p.Name.EndsWith(".Core", StringComparison.Ordinal)) && !Projects.Any(p => p.Layer == ProjectLayer.Domain))
            return "legacy";
        if (Projects.Any(p => p.File.StartsWith("src/", StringComparison.OrdinalIgnoreCase)))
            return "v2";
        return Projects.Count == 0 ? "none" : "flat";
    }

    private static IEnumerable<string> Enumerate(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(dir))
                yield return System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (!SkippedDirectories.Contains(System.IO.Path.GetFileName(sub)))
                    pending.Push(sub);
            }
        }
    }

    private ProjectInfo ParseProject(string relativeFile)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(relativeFile);
        var dir = System.IO.Path.GetDirectoryName(relativeFile)?.Replace('\\', '/') ?? string.Empty;
        var isTest = name.EndsWith(".Tests", StringComparison.Ordinal) || name.EndsWith(".Test", StringComparison.Ordinal);
        var refs = new List<string>();
        var packages = new List<PackageReferenceInfo>();
        var nullable = false;
        var twae = false;
        try
        {
            var doc = XDocument.Load(System.IO.Path.Combine(Root, relativeFile));
            foreach (var p in doc.Descendants("ProjectReference"))
            {
                var include = (string?)p.Attribute("Include");
                if (!string.IsNullOrWhiteSpace(include))
                    refs.Add(include!.Replace('\\', '/'));
            }

            foreach (var p in doc.Descendants("PackageReference"))
            {
                var include = (string?)p.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include))
                    continue;
                var inline = p.Attribute("Version") != null || p.Element("Version") != null;
                packages.Add(new PackageReferenceInfo(include!, inline));
            }

            nullable = doc.Descendants("Nullable").Any(e => e.Value.Trim().Equals("enable", StringComparison.OrdinalIgnoreCase));
            twae = doc.Descendants("TreatWarningsAsErrors").Any(e => e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
        {
            // an unreadable project is reported by the structure check as having no references
        }

        return new ProjectInfo(name, dir, relativeFile, ClassifyLayer(name), isTest, refs, packages, nullable, twae);
    }

    private static ProjectLayer ClassifyLayer(string name)
    {
        var core = Regex.Replace(name, @"\.Tests?$", string.Empty);
        if (core.EndsWith(".Domain", StringComparison.Ordinal))
            return ProjectLayer.Domain;
        if (core.EndsWith(".Application", StringComparison.Ordinal))
            return ProjectLayer.Application;
        if (core.Contains(".Infrastructure", StringComparison.Ordinal))
            return ProjectLayer.Infrastructure;
        if (core.EndsWith(".Api", StringComparison.Ordinal) || core.EndsWith(".API", StringComparison.Ordinal))
            return ProjectLayer.Api;
        if (core.EndsWith(".Mcp", StringComparison.Ordinal))
            return ProjectLayer.Mcp;
        if (core.EndsWith(".Contracts", StringComparison.Ordinal))
            return ProjectLayer.Contracts;
        return ProjectLayer.Other;
    }
}
