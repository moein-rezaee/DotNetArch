namespace DotNetArch.Core.NetArch;

/// <summary>Everything the tool keeps about a project lives in <c>.net-arch/</c>; the project itself never depends on it (D-19).</summary>
public static class NetArchFiles
{
    public const string Folder = ".net-arch";
    public const string State = "project.yml";
    public const string Rules = "rules.yml";
    public const string Profile = "profile.yml";
    public const string Lock = "generated.lock";
    public const string HomeVariable = "DOTNET_ARCH_HOME";
    public const int CurrentSchema = 2;
    public const string CurrentBlueprint = "1.0.0";
}

/// <summary>State of a project (project.yml). A cache: it can always be rebuilt from the source with <c>adopt</c>.</summary>
public sealed class ProjectState
{
    public int Schema { get; set; } = NetArchFiles.CurrentSchema;

    /// <summary>Version of the standard (blueprint) the project is aligned with.</summary>
    public string Blueprint { get; set; } = NetArchFiles.CurrentBlueprint;

    public string Layout { get; set; } = "flat";

    public bool Mcp { get; set; }

    public List<string> Layers { get; set; } = new();

    /// <summary>Built-in opt-in rule sets that apply to the project (for example <c>abp</c>).</summary>
    public List<string> Standards { get; set; } = new();

    public Dictionary<string, string> Kits { get; set; } = new();

    public Dictionary<string, EntityState> Entities { get; set; } = new();

    public Dictionary<string, string> Modules { get; set; } = new();
}

public sealed class EntityState
{
    public List<string> Slices { get; set; } = new();
}

/// <summary>Rule settings (rules.yml): severity overrides, accepted exceptions and thresholds. Global and project files use the same shape.</summary>
public sealed class RuleSettings
{
    /// <summary>Rule id to <c>off|info|warning|error</c>.</summary>
    public Dictionary<string, string> Severity { get; set; } = new();

    public List<RuleException> Exceptions { get; set; } = new();

    public Dictionary<string, double> Thresholds { get; set; } = new();
}

/// <summary>An accepted deviation: the finding is not an error because of <see cref="Reason"/> (never empty).</summary>
public sealed class RuleException
{
    public string Rule { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional path prefix; when empty the exception covers every location of the rule.</summary>
    public string? Path { get; set; }
}

/// <summary>
/// A profile (profile.yml) adds organisation-specific rules without touching the tool. The file either defines rules inline or points
/// to a shared file with <see cref="Source"/> (path relative to the repository root).
/// </summary>
public sealed class ProfileDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string? Source { get; set; }

    /// <summary>Layouts the profile accepts without a warning (for example <c>flat</c> and <c>v2</c>).</summary>
    public List<string> AcceptedLayouts { get; set; } = new();

    /// <summary>Built-in rule sets the profile switches on for every project that uses it (for example <c>abp</c>).</summary>
    public List<string> Standards { get; set; } = new();

    /// <summary>Severity the profile gives to rule ids (including built-in ones); a project's rules.yml still wins.</summary>
    public Dictionary<string, string> Severity { get; set; } = new();

    public List<ProfileRule> Rules { get; set; } = new();

    /// <summary>How the generated typed client talks HTTP; by default it uses <c>HttpClient</c>.</summary>
    public ClientTransport? Client { get; set; }

    /// <summary>Root folders that belong somewhere else in the standard layout (for example a folder of deploy-time data that belongs under <c>etc/</c>). <c>fix --rules=DA-A12</c> moves them and follows every path that points at them.</summary>
    public List<FolderMove> FolderMoves { get; set; } = new();
}

/// <summary>One folder move of a profile: <c>from</c> a repository-root folder <c>to</c> its standard place.</summary>
public sealed class FolderMove
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;
}

/// <summary>
/// A string-returning REST client abstraction that the generated typed client uses instead of <c>HttpClient</c>. The shape is fixed:
/// <c>Task&lt;string&gt; GetAsync(string path, IDictionary&lt;string,string&gt;? headers, IDictionary&lt;string,string?&gt;? query, CancellationToken ct)</c>,
/// <c>PostAsync/PutAsync/PatchAsync(string path, object? body, headers, query, ct)</c> and <c>DeleteAsync(path, headers, query, ct)</c>.
/// </summary>
public sealed class ClientTransport
{
    /// <summary><c>httpclient</c> (default) or <c>rest-client</c>.</summary>
    public string Transport { get; set; } = "httpclient";

    /// <summary>Name of the abstraction the client constructors take.</summary>
    public string Interface { get; set; } = string.Empty;

    /// <summary>Namespace of the abstraction.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>NuGet package that provides it (referenced by the client project).</summary>
    public string Package { get; set; } = string.Empty;
}

/// <summary>
/// One declarative rule. Kinds: <c>require-files</c> (Files; a trailing slash means "folder has files"), <c>forbid-project-reference</c> (Pattern),
/// <c>dockerfile-forbid-line</c> (Prefix), <c>folder-prefix</c> (Prefix), <c>note-if-files-match</c> (Pattern).
/// </summary>
public sealed class ProfileRule
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Severity { get; set; } = "warning";

    public string Message { get; set; } = string.Empty;

    public string? Hint { get; set; }

    public List<string> Files { get; set; } = new();

    public string? Pattern { get; set; }

    public string? Prefix { get; set; }
}

public sealed class GeneratedLock
{
    public Dictionary<string, string> Files { get; set; } = new();
}
