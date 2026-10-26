using System.Text;
using System.Text.RegularExpressions;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>One persisted property of an entity: <c>Name:Type[(max)][?][!#~]</c>. <c>!</c> adds a unique index, <c>#</c> a plain index, <c>~</c> keeps it out of the DTO (a hash, a secret); <c>(max)</c> is the length of a string.</summary>
internal sealed partial record EntityProperty(string Name, string Type, bool IsOptional, int? MaxLength, bool IsUnique, bool IsIndexed, bool IsHidden = false)
{
    private static readonly HashSet<string> Scalars = new(StringComparer.Ordinal)
    {
        "string", "int", "long", "bool", "decimal", "double", "float", "Guid", "DateTime", "DateTimeOffset", "TimeSpan",
    };

    public bool IsString => Type == "string";

    public bool IsScalar => Scalars.Contains(Type);

    /// <summary>Not a scalar: the name of an enum in <c>{App}.Domain.Enums</c>.</summary>
    public bool IsEnum => !IsScalar;

    public string ParameterName => char.ToLowerInvariant(Name[0]) + Name[1..];

    public string ClrType => IsOptional ? (IsString ? "string?" : Type + "?") : Type;

    public string LengthConstant => $"{Name}MaxLength";

    public int StringLength => MaxLength ?? 256;

    /// <summary>A literal that is valid for the property, used by the generated tests.</summary>
    public string Sample(int seed = 1) => Type switch
    {
        "string" => $"\"{SampleText(seed)}\"",
        "int" => seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "long" => $"{seed}L",
        "bool" => "true",
        "decimal" => $"{seed}m",
        "double" => $"{seed}.5",
        "float" => $"{seed}.5f",
        "Guid" => $"Guid.Parse(\"{seed:D8}-0000-0000-0000-000000000001\")",
        "DateTime" => "new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)",
        "DateTimeOffset" => "new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)",
        "TimeSpan" => "TimeSpan.FromMinutes(5)",
        _ => $"default({Type})",
    };

    private string SampleText(int seed)
    {
        var text = $"{Name.ToLowerInvariant()}-{seed}";
        return text.Length <= StringLength ? text : text[^StringLength..];
    }

    public static EntityProperty Parse(string text)
    {
        var match = Pattern().Match(text.Trim());
        if (!match.Success)
            throw new ArgumentException($"'{text}' is not 'Name:Type[(max)][?][!|#]'.");

        var name = match.Groups["name"].Value;
        var type = match.Groups["type"].Value;
        var isString = type == "string";
        if (!isString && match.Groups["len"].Success)
            throw new ArgumentException($"A length applies to string properties only ('{text}').");
        if (!Scalars.Contains(type) && !(char.IsUpper(type[0]) && Regex.IsMatch(type, "^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))))
            throw new ArgumentException($"Type '{type}' is not supported. Use string, int, long, bool, decimal, double, float, Guid, DateTime, DateTimeOffset, TimeSpan or the name of an enum.");
        if (name is "Id" or "CreatedAtUtc" or "UpdatedAtUtc")
            throw new ArgumentException($"'{name}' is provided by the entity base class.");

        var flags = match.Groups["flags"].Value;
        return new EntityProperty(
            name,
            type,
            match.Groups["optional"].Success,
            match.Groups["len"].Success ? int.Parse(match.Groups["len"].Value, System.Globalization.CultureInfo.InvariantCulture) : null,
            flags.Contains('!'),
            flags.Contains('#'),
            flags.Contains('~'));
    }

    public static IReadOnlyList<EntityProperty> ParseList(string? text)
    {
        var properties = UseCaseSpec.SplitTopLevel(text).Select(Parse).ToList();
        if (properties.Count == 0)
            throw new ArgumentException("An entity needs at least one property: --properties=Name:string(200),...");
        var duplicate = properties.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Property '{duplicate.Key}' is listed twice.");
        return properties;
    }

    [GeneratedRegex(@"^(?<name>[A-Z][A-Za-z0-9_]*):(?<type>[A-Za-z][A-Za-z0-9_]*)(\((?<len>\d{1,4})\))?(?<optional>\?)?(?<flags>[!#~]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
