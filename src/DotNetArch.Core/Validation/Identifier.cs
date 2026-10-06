using System.Text.RegularExpressions;

namespace DotNetArch.Core.Validation;

/// <summary>Validation and normalisation for every user-supplied name that ends up in code, paths or command lines.</summary>
public static partial class Identifier
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SimpleName();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)*$")]
    private static partial Regex DottedName();

    [GeneratedRegex("[^A-Za-z0-9_]")]
    private static partial Regex Disallowed();

    /// <summary>Removes every character that is not a letter, digit or underscore (legacy behaviour for entity/event names).</summary>
    public static string Sanitize(string? value) => Disallowed().Replace(value ?? string.Empty, string.Empty);

    /// <summary>C#-style identifier (entity, event, enum, constant, action names).</summary>
    public static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && SimpleName().IsMatch(value);

    /// <summary>Solution-style name: identifiers separated by dots (e.g. <c>Acme.Billing</c>).</summary>
    public static bool IsValidSolutionName(string? value) => !string.IsNullOrEmpty(value) && DottedName().IsMatch(value);

    public static string RequireSolutionName(string? value)
    {
        if (!IsValidSolutionName(value))
            throw new ArgumentException($"'{value}' is not a valid solution name. Use letters, digits and underscores; dots may separate segments; it must start with a letter.", nameof(value));
        return value!;
    }

    /// <summary>Throws when <paramref name="value"/> is non-blank and not a valid identifier; blank values are left to the caller's own "required" message.</summary>
    public static void RequireIfPresent(string? value, string what)
    {
        if (!string.IsNullOrWhiteSpace(value))
            RequireIdentifier(value, what);
    }

    /// <summary>Rejects blank paths and paths containing control characters (NUL, newlines) before they reach the file system or a command line.</summary>
    public static string RequirePath(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new ArgumentException($"'{value}' is not a valid {what}.", nameof(value));
        return value;
    }

    public static string RequireIdentifier(string? value, string what)
    {
        if (!IsValid(value))
            throw new ArgumentException($"'{value}' is not a valid {what}. Use letters, digits and underscores; it must not start with a digit.", nameof(value));
        return value!;
    }
}
