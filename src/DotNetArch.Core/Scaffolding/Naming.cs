namespace DotNetArch.Core.Scaffolding;

public static class Naming
{
    public static string Pluralize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith("y") && name.Length > 1 && !IsVowel(lower[lower.Length - 2]))
            return name.Substring(0, name.Length - 1) + "ies";
        if (lower.EndsWith("s") || lower.EndsWith("x") || lower.EndsWith("z") ||
            lower.EndsWith("ch") || lower.EndsWith("sh"))
            return name + "es";
        return name + "s";
    }

    /// <summary>PascalCase to kebab-case (<c>OrderItems</c> becomes <c>order-items</c>), used for routes.</summary>
    public static string ToKebabCase(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();

    static bool IsVowel(char c) => "aeiou".IndexOf(c) >= 0;
}

