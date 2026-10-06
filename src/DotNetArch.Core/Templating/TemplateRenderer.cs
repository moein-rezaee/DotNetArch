using System.Reflection;
using System.Text.RegularExpressions;

namespace DotNetArch.Core.Templating;

/// <summary>
/// Renders embedded text templates. Tokens look like <c>{{Name}}</c>; an unknown token is an error so typos never ship.
/// Template resources live under <c>Templates/</c> with the logical name equal to their relative path (forward slashes).
/// </summary>
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9_.]*)\}\}")]
    private static partial Regex Token();

    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    public static string Load(string templatePath)
    {
        var name = "Templates/" + templatePath.Replace('\\', '/');
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Template '{templatePath}' is not embedded in {Assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string Render(string template, IReadOnlyDictionary<string, string> tokens) =>
        Token().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return tokens.TryGetValue(key, out var value)
                ? value
                : throw new InvalidOperationException($"Template token '{{{{{key}}}}}' has no value.");
        });

    public static string RenderTemplate(string templatePath, IReadOnlyDictionary<string, string> tokens) =>
        Render(Load(templatePath), tokens);

    /// <summary>All embedded template paths under <paramref name="folder"/> (relative to <c>Templates/</c>), sorted.</summary>
    public static IReadOnlyList<string> List(string folder)
    {
        var prefix = "Templates/" + folder.Trim('/').Replace('\\', '/') + "/";
        return Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => n["Templates/".Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }
}
