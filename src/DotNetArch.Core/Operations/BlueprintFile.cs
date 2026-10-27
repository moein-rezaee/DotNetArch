using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DotNetArch.Core.NetArch;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetArch.Core.Operations;

internal sealed class Blueprint
{
    /// <summary>The format of this file; the tool refuses a format newer than it knows.</summary>
    public int? Schema { get; set; }

    /// <summary>The version of the model the file describes (semver): it is raised whenever the file changes.</summary>
    public string? Version { get; set; }

    public List<string> Profiles { get; set; } = new();

    public List<BlueprintEnum> Enums { get; set; } = new();

    public List<BlueprintEntity> Entities { get; set; } = new();

    public List<BlueprintFeature> Features { get; set; } = new();

    public List<BlueprintUseCase> UseCases { get; set; } = new();

    public List<BlueprintService> Services { get; set; } = new();

    public List<string> Layers { get; set; } = new();
}

internal sealed class BlueprintEnum
{
    public string? Name { get; set; }

    public string? Entity { get; set; }

    public List<string> Members { get; set; } = new();
}

internal sealed class BlueprintEntity
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Properties { get; set; }
}

internal sealed class BlueprintFeature
{
    public string? Name { get; set; }

    public string? Description { get; set; }
}

internal sealed class BlueprintUseCase
{
    public string? Entity { get; set; }

    public string? Action { get; set; }

    public string? Method { get; set; }

    public string? Input { get; set; }

    public string? Output { get; set; }

    public bool Anonymous { get; set; }

    public string? Description { get; set; }
}

internal sealed class BlueprintService
{
    public string? Name { get; set; }

    public string? Entity { get; set; }

    public string? Lifetime { get; set; }
}

/// <summary>What <c>apply_blueprint</c> last applied: the file, its format, its version and its content hash.</summary>
internal sealed class BlueprintLock
{
    public string? File { get; set; }

    public int Schema { get; set; }

    public string? Version { get; set; }

    public string? Sha256 { get; set; }
}

/// <summary>Reading, versioning and recording of a service blueprint (R-J9, R-J11).</summary>
internal static class BlueprintFile
{
    /// <summary>The newest blueprint format this tool reads.</summary>
    public const int SupportedSchema = 1;

    public const string DefaultPath = "docs/specs/blueprint.yaml";

    public const string LockFile = "service-blueprint.lock";

    private static readonly Regex SemVer = new(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly IDeserializer Reader = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

    private static readonly ISerializer Writer = new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();

    public static Blueprint Read(string file, out string sha256)
    {
        if (!File.Exists(file))
            throw new ArgumentException($"Blueprint file not found: {file}");
        var text = File.ReadAllText(file);
        sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ReplaceLineEndings("\n")))).ToLowerInvariant();
        try
        {
            return Reader.Deserialize<Blueprint>(text) ?? throw new ArgumentException($"Blueprint is empty: {file}");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ArgumentException($"Invalid blueprint {file}: {ex.Message}");
        }
    }

    /// <summary>The format must be known and the version must be a semver; both are written in the file so a tool of another age does not misread it.</summary>
    public static Version Validate(Blueprint blueprint, string file)
    {
        if (blueprint.Schema is null)
            throw new ArgumentException($"The blueprint {file} needs 'schema: {SupportedSchema}' (the format of the file).");
        if (blueprint.Schema is < 1 || blueprint.Schema > SupportedSchema)
            throw new ArgumentException($"The blueprint {file} uses schema {blueprint.Schema}; this tool reads schema {SupportedSchema} at most. Upgrade the tool.");
        if (string.IsNullOrWhiteSpace(blueprint.Version) || !SemVer.IsMatch(blueprint.Version))
            throw new ArgumentException($"The blueprint {file} needs 'version: <major.minor.patch>' (the version of the model).");
        return System.Version.Parse(blueprint.Version);
    }

    public static BlueprintLock? LoadLock(string root)
    {
        var path = Path.Combine(root, NetArchFiles.Folder, LockFile);
        return File.Exists(path) ? Reader.Deserialize<BlueprintLock>(File.ReadAllText(path)) : null;
    }

    public static void SaveLock(string root, BlueprintLock state)
    {
        var path = Path.Combine(root, NetArchFiles.Folder, LockFile);
        var text = Writer.Serialize(state);
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    /// <summary>The blueprint of a repository: the one the lock names, else the default path when it exists.</summary>
    public static string? FindFile(string root)
    {
        var recorded = LoadLock(root)?.File;
        if (!string.IsNullOrWhiteSpace(recorded) && File.Exists(Path.Combine(root, recorded)))
            return Path.Combine(root, recorded);
        var fallback = Path.Combine(root, DefaultPath);
        return File.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// A blueprint only grows forward: a lower version than the applied one is refused, and so is a changed file under the same version, so every
    /// change of the model carries a new version.
    /// </summary>
    public static void EnsureVersionDiscipline(BlueprintLock? applied, string relativePath, Version version, string sha256)
    {
        if (applied is null || !string.Equals(applied.File, relativePath, StringComparison.Ordinal) || applied.Version is null)
            return;
        var previous = System.Version.Parse(applied.Version);
        if (version < previous)
            throw new ArgumentException($"The blueprint version {version} is lower than the applied version {previous}; a blueprint only moves forward.");
        if (version == previous && !string.Equals(applied.Sha256, sha256, StringComparison.Ordinal))
            throw new ArgumentException($"The blueprint changed but its version is still {version}; raise 'version' (the model changed).");
    }
}
