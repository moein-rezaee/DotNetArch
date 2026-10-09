using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetArch.Core.NetArch;

/// <summary>Reads and writes the <c>.net-arch/</c> files of a project and the global <c>~/.net-arch/</c> settings.</summary>
public static class NetArchStore
{
    private static readonly IDeserializer Reader = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Writer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public static string ProjectFolder(string root) => Path.Combine(root, NetArchFiles.Folder);

    /// <summary>Global settings folder; <c>DOTNET_ARCH_HOME</c> overrides the default <c>~/.net-arch</c> (used by tests and CI).</summary>
    public static string GlobalFolder()
    {
        var overridden = Environment.GetEnvironmentVariable(NetArchFiles.HomeVariable);
        return !string.IsNullOrWhiteSpace(overridden)
            ? overridden
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), NetArchFiles.Folder);
    }

    public static ProjectState? LoadState(string root) => Read<ProjectState>(Path.Combine(ProjectFolder(root), NetArchFiles.State));

    public static RuleSettings LoadRules(string folder) => Read<RuleSettings>(Path.Combine(folder, NetArchFiles.Rules)) ?? new RuleSettings();

    public static ProfileDefinition? LoadProfileFile(string path) => Read<ProfileDefinition>(path);

    /// <summary>Rules after merging tool defaults (none) &lt; global &lt; project; the project only stores differences.</summary>
    public static RuleSettings LoadEffectiveRules(string root)
    {
        var global = LoadRules(GlobalFolder());
        var project = LoadRules(ProjectFolder(root));
        var merged = new RuleSettings();
        foreach (var source in new[] { global, project })
        {
            foreach (var (key, value) in source.Severity)
                merged.Severity[key] = value;
            foreach (var (key, value) in source.Thresholds)
                merged.Thresholds[key] = value;
            merged.Exceptions.AddRange(source.Exceptions);
        }

        return merged;
    }

    /// <summary>Resolves the profile: an explicit file, else the project's <c>profile.yml</c> (following its <c>source</c>), else none.</summary>
    public static ProfileDefinition? ResolveProfile(string root, string? explicitPath, out string? warning)
    {
        warning = null;
        var path = explicitPath ?? Path.Combine(ProjectFolder(root), NetArchFiles.Profile);
        if (!File.Exists(path))
        {
            if (explicitPath != null)
                warning = $"Profile file not found: {explicitPath}";
            return null;
        }

        var profile = LoadProfileFile(path);
        if (profile == null)
        {
            warning = $"Profile file is empty or unreadable: {path}";
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Source))
            return profile;

        var sourcePath = Path.GetFullPath(Path.Combine(root, profile.Source));
        var shared = File.Exists(sourcePath) ? LoadProfileFile(sourcePath) : null;
        if (shared == null)
        {
            warning = $"Profile source not found: {profile.Source} (continuing without profile rules; the project does not depend on it)";
            return null;
        }

        if (!string.IsNullOrWhiteSpace(profile.Version) && !string.Equals(profile.Version, shared.Version, StringComparison.Ordinal))
            warning = $"Profile '{shared.Name}' is version {shared.Version} but the project pins {profile.Version}.";
        return shared;
    }

    public static void SaveState(string root, ProjectState state) => Write(Path.Combine(ProjectFolder(root), NetArchFiles.State), state);

    public static void SaveLock(string root, GeneratedLock lockFile) => Write(Path.Combine(ProjectFolder(root), NetArchFiles.Lock), lockFile);

    public static string ToYaml(object value) => Writer.Serialize(value);

    private static T? Read<T>(string path)
        where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return Reader.Deserialize<T>(File.ReadAllText(path));
        }
        catch (YamlDotNet.Core.YamlException)
        {
            throw new ArgumentException($"Invalid YAML in {path}");
        }
    }

    private static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Writer.Serialize(value));
    }
}
