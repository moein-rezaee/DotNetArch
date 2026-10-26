using System.Text;
using System.Text.RegularExpressions;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>One property of a typed use case: <c>Name:Type</c>, where a trailing <c>?</c> on the type makes it optional.</summary>
internal sealed record UseCaseProperty(string Name, string Type, int? MaxLength = null)
{
    public bool IsOptional => Type.EndsWith('?');

    public string ParameterName => char.ToLowerInvariant(Name[0]) + Name[1..];

    public string Declaration => $"{Type} {Name}";

    public string Description => Regex.Replace(Name, "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant();
}

/// <summary>
/// A typed use case (<c>new action --input=... --output=...</c>): the request properties and the result of an operation that is not the "do
/// something to one aggregate by id" shape. Input is a list <c>Name:Type,Name:Type</c>; output is a list, a type expression, <c>none</c> or empty.
/// Types are restricted to scalars, arrays/lists/dictionaries of scalars and simple DTO names so the generated contract, controller, MCP tool and
/// CLI command stay expressible by the typed-client generator.
/// </summary>
internal sealed record UseCaseSpec(IReadOnlyList<UseCaseProperty> Input, UseCaseOutput Output)
{
    private static readonly HashSet<string> Scalars = new(StringComparer.Ordinal)
    {
        "string", "int", "long", "bool", "decimal", "double", "float", "Guid", "DateTime", "DateTimeOffset", "TimeSpan",
    };

    /// <summary>The length a string input is validated against when the property gives none (<c>Name:string(200)</c>).</summary>
    public const int DefaultStringLength = 512;

    private static readonly Regex LengthSuffix = new(@"^string\((\d{1,5})\)(\?)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static UseCaseSpec Parse(string? input, string? output)
    {
        var properties = SplitTopLevel(input).Select(ParseProperty).ToList();
        var duplicate = properties.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Input property '{duplicate.Key}' is listed twice.");
        return new UseCaseSpec(properties, UseCaseOutput.Parse(output));
    }

    public bool HasId => Input.Any(p => p.Name.Equals("Id", StringComparison.Ordinal));

    /// <summary>Record parameters, required ones first so optional ones can carry a default.</summary>
    public string RecordParameters() => string.Join(", ", Input.OrderBy(p => p.IsOptional).Select(p => p.Declaration));

    /// <summary>Method parameters for an MCP tool or CLI handler: required first, optional with <c>= default</c>.</summary>
    public string MethodParameters(Func<UseCaseProperty, string> attribute) =>
        string.Join(", ", Input.OrderBy(p => p.IsOptional).Select(p => $"{attribute(p)}{p.Type} {p.ParameterName}{(p.IsOptional ? " = default" : string.Empty)}"));

    /// <summary>Named arguments for the record constructor, independent of parameter order.</summary>
    public string NamedArguments(string prefix = "") =>
        string.Join(", ", Input.Select(p => $"{p.Name}: {prefix}{p.ParameterName}"));

    public string ValidatorRules()
    {
        var rules = new StringBuilder();
        foreach (var property in Input.Where(p => !p.IsOptional))
        {
            var rule = property.Type switch
            {
                "string" => $"NotEmpty().MaximumLength({property.MaxLength ?? DefaultStringLength})",
                "Guid" => "NotEmpty()",
                _ when property.Type.EndsWith("[]", StringComparison.Ordinal) || property.Type.StartsWith("List<", StringComparison.Ordinal) || property.Type.StartsWith("Dictionary<", StringComparison.Ordinal) => "NotNull()",
                _ => null,
            };
            if (rule is not null)
                rules.AppendLine($"        RuleFor(command => command.{property.Name}).{rule};");
        }

        return rules.ToString().TrimEnd('\n', '\r');
    }

    private static UseCaseProperty ParseProperty(string text)
    {
        var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            throw new ArgumentException($"'{text}' is not 'Name:Type'.");
        if (!Identifier.IsMatch(parts[0]) || !char.IsUpper(parts[0][0]))
            throw new ArgumentException($"Property name '{parts[0]}' must be PascalCase letters, digits and underscores.");
        int? maxLength = null;
        var lengthMatch = LengthSuffix.Match(parts[1].Replace(" ", string.Empty));
        if (lengthMatch.Success)
        {
            maxLength = int.Parse(lengthMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (maxLength is < 1)
                throw new ArgumentException($"The length in '{text}' must be at least 1.");
            parts[1] = "string" + lengthMatch.Groups[2].Value;
        }

        if (!IsAllowedType(parts[1]))
            throw new ArgumentException($"Type '{parts[1]}' is not supported. Use string, int, long, bool, decimal, double, float, Guid, DateTime, DateTimeOffset, TimeSpan, T[], List<T> or Dictionary<string, T> of those (a trailing ? makes a type optional), or a DTO name.");
        return new UseCaseProperty(parts[0], parts[1].Replace(" ", string.Empty), maxLength);
    }

    internal static bool IsAllowedType(string type)
    {
        var text = type.Replace(" ", string.Empty);
        if (text.EndsWith('?'))
            text = text[..^1];
        if (text.EndsWith("[]", StringComparison.Ordinal))
            return IsAllowedType(text[..^2]);
        if (text.StartsWith("List<", StringComparison.Ordinal) && text.EndsWith('>'))
            return IsAllowedType(text[5..^1]);
        if (text.StartsWith("Dictionary<string,", StringComparison.Ordinal) && text.EndsWith('>'))
            return IsAllowedType(text["Dictionary<string,".Length..^1]);
        return Scalars.Contains(text) || (Identifier.IsMatch(text) && char.IsUpper(text[0]));
    }

    internal static IEnumerable<string> SplitTopLevel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return text[start..i].Trim();
                    start = i + 1;
                    break;
            }
        }

        yield return text[start..].Trim();
    }
}

/// <summary>What a typed use case returns: a generated result record, an existing type, or nothing.</summary>
internal sealed record UseCaseOutput(UseCaseOutputKind Kind, string Type, IReadOnlyList<UseCaseProperty> Properties)
{
    public static UseCaseOutput Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new UseCaseOutput(UseCaseOutputKind.EntityDto, string.Empty, Array.Empty<UseCaseProperty>());
        if (text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return new UseCaseOutput(UseCaseOutputKind.None, string.Empty, Array.Empty<UseCaseProperty>());
        if (text.Contains(':'))
        {
            var properties = UseCaseSpec.Parse(text, null).Input;
            return new UseCaseOutput(UseCaseOutputKind.Generated, string.Empty, properties);
        }

        var type = text.Trim().Replace(" ", string.Empty);
        if (!UseCaseSpec.IsAllowedType(type))
            throw new ArgumentException($"Output type '{text}' is not supported.");
        return new UseCaseOutput(UseCaseOutputKind.Existing, type, Array.Empty<UseCaseProperty>());
    }
}

internal enum UseCaseOutputKind
{
    /// <summary>No output option: the entity DTO (the classic action).</summary>
    EntityDto,

    /// <summary>A result record generated from a property list.</summary>
    Generated,

    /// <summary>An existing type or a scalar/collection type.</summary>
    Existing,

    /// <summary>No result.</summary>
    None,
}
