using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>rename_symbol</c>: renames one identifier everywhere it is used in the source of the repository (a property, a method, a type, a constant). A whole-word match
/// only, so <c>AccountingProviderKey</c> does not touch <c>AccountingProviderKeys</c>. It is a text rename, not a semantic one: the build tells what it missed.
/// </summary>
internal static partial class AdapterOperations
{
    public static OperationDefinition RenameSymbol { get; } = new(
        "rename_symbol",
        "Rename one identifier (whole word) in every C# source file of the repository, strings included, so a configuration key named after a property moves with it. With 'text' also in the settings, schema and document files (json, yaml, md). A file that already uses the new name is reported as manual and left unchanged unless merge is true. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(),
            new OperationParameter("from", "The identifier to rename.", Required: true),
            new OperationParameter("to", "The new identifier.", Required: true),
            new OperationParameter("text", "true to rename in json, yaml and md files too (settings, schemas, documents)."),
            new OperationParameter("merge", "true to rename in a file that already uses the new name too (two different members may share one name; the build tells if they clash)."),
        },
        RunRenameSymbol);

    private static OperationResult RunRenameSymbol(OperationRequest request)
    {
        var root = Root(request);
        var ctx = Context(root);
        var from = request.Get("from") ?? throw new ArgumentException("Missing required value 'from'.");
        var to = request.Get("to") ?? throw new ArgumentException("Missing required value 'to'.");
        var identifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!identifier.IsMatch(from) || !identifier.IsMatch(to))
            throw new ArgumentException("'from' and 'to' are identifiers (letters, digits, underscore).");
        if (from == to)
            throw new ArgumentException("'from' and 'to' are the same.");

        var pattern = new Regex(@"\b" + Regex.Escape(from) + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        var target = new Regex(@"\b" + Regex.Escape(to) + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        var merge = string.Equals(request.Get("merge"), "true", StringComparison.OrdinalIgnoreCase);
        var withText = string.Equals(request.Get("text"), "true", StringComparison.OrdinalIgnoreCase);
        var extensions = withText ? new[] { ".cs", ".json", ".yaml", ".yml", ".md" } : new[] { ".cs" };
        var plan = new List<FixAction>();
        var manual = new List<string>();
        foreach (var file in ctx.Files.Where(f => extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                                                   && !f.Contains("/obj/", StringComparison.Ordinal) && !f.Contains("/bin/", StringComparison.Ordinal)))
        {
            var text = ctx.Read(file);
            if (!pattern.IsMatch(text))
                continue;
            if (!merge && target.IsMatch(text))
            {
                manual.Add($"{file}: already uses the name {to}; renamed there would merge two names, so the file is left unchanged.");
                continue;
            }

            var count = pattern.Matches(text).Count;
            // a file named after the identifier (the usual one-type file) follows the rename
            var stem = Path.GetFileNameWithoutExtension(file);
            var path = file;
            if (stem.Equals(from, StringComparison.Ordinal) && file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                path = (Path.GetDirectoryName(file)!.Length == 0 ? string.Empty : Path.GetDirectoryName(file)!.Replace('\\', '/') + "/") + to + ".cs";
                if (ctx.Files.Contains(path, StringComparer.Ordinal))
                {
                    manual.Add($"{file}: {path} already exists; the file is not renamed.");
                    path = file;
                }
                else
                {
                    plan.Add(new FixAction(new PlannedChange(path, "move", $"file follows the rename ({file})", "rename_symbol"), string.Empty, MoveFrom: file));
                }
            }

            plan.Add(new FixAction(new PlannedChange(path, "modify", $"{from} -> {to} ({count})", "rename_symbol"), pattern.Replace(text, to)));
        }

        if (plan.Count == 0 && manual.Count == 0)
            manual.Add($"{from} is not used in any source file.");
        return Planned("rename_symbol", root, plan, request.Apply, manual);
    }
}
