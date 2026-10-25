using System.Globalization;
using System.Text.Json;

namespace {{App}}.Cli;

/// <summary>A usage error: wrong command, missing or malformed option. Exit code 2.</summary>
public sealed class CliUsageException(string message) : Exception(message);

/// <summary>The options of one command line (<c>--name=value</c>, <c>--name value</c>, <c>--flag</c>) with typed access.</summary>
public sealed class CliContext(IReadOnlyDictionary<string, string> values)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool Has(string name) => values.ContainsKey(name);

    public T Require<T>(string name) =>
        values.TryGetValue(name, out var text) ? (T)Convert(name, text, typeof(T))! : throw new CliUsageException($"Missing option --{name}.");

    /// <summary>The option, or the default of <typeparamref name="T"/> when it was not given.</summary>
    public T? Optional<T>(string name) =>
        values.TryGetValue(name, out var text) ? (T?)Convert(name, text, typeof(T)) : default;

    private static object? Convert(string name, string text, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (target == typeof(string))
                return text;
            if (target == typeof(Guid))
                return Guid.Parse(text, CultureInfo.InvariantCulture);
            if (target == typeof(bool))
                return text.Length == 0 || bool.Parse(text);
            if (target == typeof(DateTime))
                return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (target == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (target == typeof(TimeSpan))
                return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
            if (target.IsPrimitive || target == typeof(decimal))
                return System.Convert.ChangeType(text, target, CultureInfo.InvariantCulture);

            return JsonSerializer.Deserialize(text, target, JsonOptions) ?? throw new JsonException("null");
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or JsonException or InvalidCastException)
        {
            throw new CliUsageException($"Option --{name}: '{text}' is not a valid {target.Name}.");
        }
    }
}
