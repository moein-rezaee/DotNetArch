namespace DotNetArch.Core.Operations;

/// <summary>
/// The packages the .NET shared frameworks already provide, read from the installed SDK (<c>packs/*/&lt;version&gt;/data/PackageOverrides.txt</c>, lines
/// <c>Id|Version</c>). From .NET 10 the SDK reports a reference to one of them as NU1510, so the migration removes it.
/// </summary>
internal sealed class FrameworkPackages
{
    private readonly Dictionary<string, string> _netCore;
    private readonly Dictionary<string, string> _aspNet;

    private FrameworkPackages(Dictionary<string, string> netCore, Dictionary<string, string> aspNet)
    {
        _netCore = netCore;
        _aspNet = aspNet;
    }

    /// <summary>The newest installed pack of the target major version, searched in <paramref name="hint"/> alone when it is given, otherwise DOTNET_ROOT, the dotnet on the path and the usual folders.</summary>
    public static FrameworkPackages? Find(int target, string? hint)
    {
        foreach (var root in Roots(hint))
        {
            var netCore = Newest(root, "Microsoft.NETCore.App.Ref", target);
            if (netCore is null)
                continue;
            return new FrameworkPackages(Read(netCore), Newest(root, "Microsoft.AspNetCore.App.Ref", target) is { } web ? Read(web) : new Dictionary<string, string>());
        }

        return null;
    }

    /// <summary>
    /// The runtime version of the target major that is installed (for example <c>10.0.14</c>), searched like <see cref="Find"/>. The runtime-shipped
    /// packages (<c>Microsoft.Extensions.*</c>, <c>Microsoft.AspNetCore.*</c>, <c>Microsoft.EntityFrameworkCore*</c>) are moved to this version, so the whole
    /// family matches and a package that pulls an older patch of its neighbours cannot cause a downgrade error or a known-vulnerability error.
    /// </summary>
    public static string? InstalledRuntime(int target, string? hint)
    {
        foreach (var root in Roots(hint))
        {
            var shared = Path.Combine(root, "shared", "Microsoft.NETCore.App");
            if (!Directory.Exists(shared))
                continue;
            var newest = Directory.EnumerateDirectories(shared, target + ".*")
                .Select(d => Path.GetFileName(d))
                .Where(name => Version.TryParse(Core(name), out _))
                .OrderByDescending(name => Version.Parse(Core(name)))
                .FirstOrDefault();
            if (newest is not null && !newest.Contains('-', StringComparison.Ordinal))
                return newest;
        }

        return null;
    }

    public bool Provides(string id, bool web, out string version)
    {
        if (_netCore.TryGetValue(id, out var shipped) || (web && _aspNet.TryGetValue(id, out shipped)))
        {
            version = shipped;
            return true;
        }

        version = string.Empty;
        return false;
    }

    /// <summary>True when <paramref name="version"/> is not higher than <paramref name="shipped"/> (an unknown version counts as not higher).</summary>
    public static bool IsAtMost(string? version, string shipped)
    {
        if (string.IsNullOrWhiteSpace(version) || !Version.TryParse(Core(version), out var have) || !Version.TryParse(Core(shipped), out var limit))
            return true;
        return have <= limit;
    }

    private static string Core(string version) => version.Split('-', '+')[0];

    private static IEnumerable<string> Roots(string? hint)
    {
        if (!string.IsNullOrWhiteSpace(hint))
            return Directory.Exists(hint) ? new[] { hint } : Array.Empty<string>();

        var roots = new List<string?> { Environment.GetEnvironmentVariable("DOTNET_ROOT") };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var exe = Path.Combine(dir, "dotnet");
            if (!File.Exists(exe))
                continue;
            var resolved = new FileInfo(exe).ResolveLinkTarget(true)?.FullName ?? exe;
            roots.Add(Path.GetDirectoryName(resolved));
        }

        roots.Add("/usr/local/share/dotnet");
        roots.Add("/usr/share/dotnet");
        roots.Add("/usr/lib/dotnet");
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"));
        return roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)).Select(r => r!).Distinct();
    }

    private static string? Newest(string root, string pack, int target)
    {
        var packs = Path.Combine(root, "packs", pack);
        if (!Directory.Exists(packs))
            return null;
        return Directory.EnumerateDirectories(packs, target + ".*")
            .Select(d => (Dir: d, Ok: Version.TryParse(Core(Path.GetFileName(d)), out var v), Version: Version.TryParse(Core(Path.GetFileName(d)), out var v2) ? v2 : new Version(0, 0)))
            .Where(x => x.Ok)
            .OrderByDescending(x => x.Version)
            .Select(x => Path.Combine(x.Dir, "data", "PackageOverrides.txt"))
            .FirstOrDefault(File.Exists);
    }

    private static Dictionary<string, string> Read(string file)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(file))
        {
            var parts = line.Split('|');
            if (parts.Length == 2 && parts[0].Length > 0)
                map[parts[0].Trim()] = parts[1].Trim();
        }

        return map;
    }
}
