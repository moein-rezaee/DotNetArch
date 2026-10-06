using System.Text.RegularExpressions;

namespace DotNetArch.Core.Scaffolding.Ops;

/// <summary>Knows the git hosting flavours and how to recognise them from a remote URL.</summary>
public static partial class GitHosts
{
    public const string GitHub = "github";
    public const string GitLab = "gitlab";
    public const string Azure = "azure";
    public const string Bitbucket = "bitbucket";
    public const string Gitea = "gitea";

    public static readonly string[] Providers = { GitHub, GitLab, Azure, Bitbucket, Gitea };

    [GeneratedRegex(@"^(?:[a-z][a-z0-9+.-]*://)?(?:[^@/]+@)?([^/:]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HostPattern();

    private static readonly string[] PublicHosts =
    {
        "github.com", "gitlab.com", "bitbucket.org", "dev.azure.com", "ssh.dev.azure.com", "gitea.com", "codeberg.org"
    };

    /// <summary>True for the well-known public services; any other host is a personal / self-hosted server.</summary>
    public static bool IsPublicHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && PublicHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownProvider(string? provider) =>
        Providers.Contains(provider ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static string? HostOf(string? remote)
    {
        if (string.IsNullOrWhiteSpace(remote))
            return null;
        var match = HostPattern().Match(remote.Trim());
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    /// <summary>Maps a remote URL (https, ssh or scp-style) to a provider id; null when the host is not recognisable.</summary>
    public static string? Detect(string? remote)
    {
        var host = HostOf(remote);
        if (host is null)
            return null;

        if (host == "github.com" || host.EndsWith(".github.com", StringComparison.Ordinal) || host.StartsWith("github.", StringComparison.Ordinal))
            return GitHub;
        if (host == "gitlab.com" || host.StartsWith("gitlab.", StringComparison.Ordinal) || host.Contains(".gitlab.", StringComparison.Ordinal))
            return GitLab;
        if (host == "dev.azure.com" || host.EndsWith("ssh.dev.azure.com", StringComparison.Ordinal) || host.EndsWith(".visualstudio.com", StringComparison.Ordinal))
            return Azure;
        if (host == "bitbucket.org" || host.StartsWith("bitbucket.", StringComparison.Ordinal))
            return Bitbucket;
        if (host == "gitea.com" || host.StartsWith("gitea.", StringComparison.Ordinal) || host.StartsWith("forgejo.", StringComparison.Ordinal))
            return Gitea;
        return null;
    }
}
