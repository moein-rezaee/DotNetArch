using System.Text;
using System.Text.RegularExpressions;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>split_registration</c> (Phase 15.8): after the adapter's code moved into its own project (<c>move_code</c>), the shared Infrastructure registration
/// method still registers it. The operation reads the method body statement by statement, moves every statement that names a type declared in the
/// adapter project into the adapter's own registration method, and leaves the rest. Statements that share a local variable across the line are not
/// guessed: they are listed as manual. Plan first; writes only with apply.
/// </summary>
internal static partial class AdapterOperations
{
    public static OperationDefinition SplitRegistration { get; } = new(
        "split_registration",
        "Move the statements of the shared Infrastructure registration method that belong to one adapter into that adapter's registration method. A statement belongs to the adapter when it names a type declared in the adapter project. The moved statements keep their order and are placed before the final return of the adapter method; the usings of the source file are copied to the adapter file. A local variable shared between moved and kept statements is listed as manual. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(),
            new OperationParameter("adapter", "Adapter name (the part after <App>.Infrastructure.).", Required: true),
            new OperationParameter("from", "Project that holds the shared registration method (default: <App>.Infrastructure)."),
            new OperationParameter("method", "Name of the shared registration method (default: the first Add...Infrastructure method)."),
            new OperationParameter("also", "Comma-separated texts (an Add... extension method of a kit, a service name in quotes) that also make a statement belong to the adapter, though it names no type of its project. A statement that contains the text moves."),
        },
        RunSplitRegistration);

    private static OperationResult RunSplitRegistration(OperationRequest request)
    {
        var root = Root(request);
        var ctx = Context(root);
        var adapterName = request.Get("adapter") ?? throw new ArgumentException("Missing required value 'adapter'.");
        var source = request.Get("from") != null
            ? Project(ctx, request.Get("from"), "from")
            : ctx.Source.FirstOrDefault(p => p.Name.EndsWith(".Infrastructure", StringComparison.Ordinal))
              ?? throw new ArgumentException("No <App>.Infrastructure project found.");
        var app = source.Name.EndsWith(".Infrastructure", StringComparison.Ordinal) ? source.Name[..^".Infrastructure".Length] : source.Name;
        var adapter = Project(ctx, $"{app}.Infrastructure.{adapterName}", "adapter");

        var sourceFile = ctx.Files.FirstOrDefault(f => f.StartsWith(source.Dir + "/", StringComparison.Ordinal) && f.EndsWith("DependencyInjection.cs", StringComparison.Ordinal))
                         ?? throw new ArgumentException($"{source.Name} has no DependencyInjection.cs.");
        var adapterFile = ctx.Files.FirstOrDefault(f => f.StartsWith(adapter.Dir + "/", StringComparison.Ordinal) && f.EndsWith("DependencyInjection.cs", StringComparison.Ordinal))
                          ?? throw new ArgumentException($"{adapter.Name} has no DependencyInjection.cs (scaffold the adapter first).");
        var sourceText = ctx.Read(sourceFile);
        var adapterText = ctx.Read(adapterFile);

        var wanted = request.Get("method");
        var sourceMethod = RegistrationMethod().Matches(sourceText).Cast<Match>()
                               .FirstOrDefault(m => wanted != null ? m.Groups["name"].Value == wanted : m.Groups["name"].Value.EndsWith("Infrastructure", StringComparison.Ordinal))
                           ?? throw new ArgumentException($"{sourceFile} has no matching public registration method.");
        var adapterMethod = RegistrationMethod().Match(adapterText);
        if (!adapterMethod.Success)
            throw new ArgumentException($"{adapterFile} has no public registration extension method.");

        var (sourceStart, sourceEnd) = BodyRange(sourceText, sourceMethod.Index + sourceMethod.Length);
        var (adapterStart, adapterEnd) = BodyRange(adapterText, adapterMethod.Index + adapterMethod.Length);
        var statements = SplitStatements(sourceText[sourceStart..sourceEnd]);

        var adapterTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in ctx.Files.Where(f => f.StartsWith(adapter.Dir + "/", StringComparison.Ordinal) && f.EndsWith(".cs", StringComparison.Ordinal) && !f.EndsWith("DependencyInjection.cs", StringComparison.Ordinal)))
            foreach (Match m in TypeDeclaration().Matches(ctx.Read(file)))
                adapterTypes.Add(m.Groups["name"].Value);

        var also = (request.Get("also") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var moved = new List<string>();
        var kept = new List<string>();
        foreach (var statement in statements)
        {
            var isReturn = Regex.IsMatch(statement, @"^\s*return\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            var code = StripComments(statement);
            var belongs = !isReturn && (Word().Matches(code).Any(w => adapterTypes.Contains(w.Value)) || also.Any(a => code.Contains(a, StringComparison.Ordinal)));
            (belongs ? moved : kept).Add(statement);
        }

        var manual = new List<string>();
        if (moved.Count == 0)
            return new OperationResult(true, $"split_registration: nothing in {sourceMethod.Groups["name"].Value} names a type of {adapter.Name}; no change.", new { manual }, Array.Empty<PlannedChange>(), false);

        var declared = (IEnumerable<string> list) => list.SelectMany(s => LocalDeclaration().Matches(s).Select(m => m.Groups["name"].Value)).ToHashSet(StringComparer.Ordinal);
        var movedLocals = declared(moved);
        var keptLocals = declared(kept);
        foreach (var local in movedLocals.Where(l => kept.Any(s => UsesWord(s, l))))
            manual.Add($"local '{local}' is declared by a moved statement and still used by a kept one in {sourceMethod.Groups["name"].Value}.");
        foreach (var local in keptLocals.Where(l => moved.Any(s => UsesWord(s, l))))
            manual.Add($"local '{local}' is declared by a kept statement and used by a moved one: declare it in {adapterMethod.Groups["name"].Value} too.");

        // private helper methods of the source class that only the moved statements use (directly or through each other) travel with them
        var helpers = PrivateHelpers(sourceText);
        var movedHelpers = HelpersThatMove(helpers, moved, kept, manual);

        var plan = new List<FixAction>();

        // adapter file: moved statements go before its final return; the usings of the source file come along
        var adapterBody = adapterText[adapterStart..adapterEnd];
        var returnAt = adapterBody.LastIndexOf("return ", StringComparison.Ordinal);
        var insertAt = returnAt < 0 ? adapterBody.Length : adapterBody.LastIndexOf('\n', returnAt) + 1;
        var newBody = adapterBody[..insertAt] + string.Concat(moved.Select(s => s.TrimEnd() + "\n")).TrimEnd('\n') + "\n\n" + adapterBody[insertAt..];
        var newAdapterText = adapterText[..adapterStart] + newBody + adapterText[adapterEnd..];
        if (movedHelpers.Count > 0)
        {
            var classEnd = newAdapterText.LastIndexOf('}');
            newAdapterText = newAdapterText[..classEnd].TrimEnd() + "\n" + string.Concat(movedHelpers.Select(h => "\n" + h.Text.TrimEnd() + "\n")) + newAdapterText[classEnd..];
        }

        var present = Using().Matches(newAdapterText).Select(m => m.Groups["ns"].Value).ToHashSet(StringComparer.Ordinal);
        var ownNamespace = NamespaceDeclaration().Match(sourceText).Groups["ns"].Value;
        var addUsings = Using().Matches(sourceText).Select(m => m.Groups["ns"].Value)
            .Where(ns => !present.Contains(ns) && (!InNamespace(ns, ownNamespace) || InNamespace(ns, adapter.Name))).Distinct().OrderBy(ns => ns, StringComparer.Ordinal).ToList();
        if (addUsings.Count > 0)
            newAdapterText = string.Concat(addUsings.Select(u => $"using {u};\n")) + newAdapterText;
        plan.Add(new FixAction(new PlannedChange(adapterFile, "modify", $"{moved.Count} registration statement(s) moved in" + (movedHelpers.Count == 0 ? string.Empty : $" with {movedHelpers.Count} helper method(s)"), "split_registration"), newAdapterText));

        // source file: the moved statements and their helpers leave
        // the closing brace keeps its indentation: the whitespace that followed the last statement stays
        var originalBody = sourceText[sourceStart..sourceEnd];
        var keptBody = string.Concat(kept) + originalBody[originalBody.TrimEnd().Length..].TrimStart('\r', '\n');
        var edits = new List<(int Start, int End, string Text)> { (sourceStart, sourceEnd, keptBody) };
        edits.AddRange(movedHelpers.Select(h => (h.Start, h.End, string.Empty)));
        var newSourceText = sourceText;
        foreach (var edit in edits.OrderByDescending(e => e.Start))
            newSourceText = newSourceText[..edit.Start] + edit.Text + newSourceText[edit.End..];
        // the shared project cannot reference the adapter, so a using of the adapter's namespace tree has no use left in it
        newSourceText = Regex.Replace(newSourceText, @"(?m)^[ \t]*using\s+" + Regex.Escape(adapter.Name) + @"(?:\.[A-Za-z_]\w*)*\s*;[ \t]*\r?\n", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        plan.Add(new FixAction(new PlannedChange(sourceFile, "modify", $"{moved.Count} registration statement(s) moved out to {adapter.Name}", "split_registration"), newSourceText));

        // the packages the copied usings need in the adapter project
        var adapterProject = Path.Combine(root, adapter.File);
        if (File.Exists(adapterProject))
        {
            var csprojText = ctx.Read(adapter.File);
            var propsText = ctx.Has("Directory.Packages.props") ? ctx.Read("Directory.Packages.props") : string.Empty;
            var added = new List<string>();
            var propsEdited = RequirePackages(ctx, root, source, adapter, addUsings, ref csprojText, ref propsText, added);
            if (added.Count > 0)
                plan.Add(new FixAction(new PlannedChange(adapter.File, "modify", string.Join(", ", added), "split_registration"), csprojText));
            if (propsEdited)
                plan.Add(new FixAction(new PlannedChange("Directory.Packages.props", "modify", "central versions for the packages the adapter now uses", "split_registration"), propsText));
        }

        manual.Add("build to verify: usings that the adapter project does not reference, and statements that depend on code left behind, show up as compile errors.");
        return Planned("split_registration", root, plan, request.Apply, manual);
    }

    private static bool UsesWord(string statement, string word) =>
        Word().Matches(StripComments(statement)).Any(w => w.Value == word);

    /// <summary>The text between the braces of the body that starts at or after <paramref name="from"/>.</summary>
    private static (int Start, int End) BodyRange(string text, int from)
    {
        var open = text.IndexOf('{', from);
        if (open < 0)
            throw new ArgumentException("Method has no body.");
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            i = SkipLiteral(text, i);
            if (i >= text.Length)
                break;
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return (open + 1, i);
        }

        throw new ArgumentException("Unbalanced braces in the registration method.");
    }

    /// <summary>If a string, char or comment starts at <paramref name="i"/>, the index of its last character; else <paramref name="i"/>.</summary>
    private static int SkipLiteral(string t, int i)
    {
        var c = t[i];
        if (c == '/' && i + 1 < t.Length && t[i + 1] == '/')
        {
            var end = t.IndexOf('\n', i);
            return end < 0 ? t.Length : end;
        }

        if (c == '/' && i + 1 < t.Length && t[i + 1] == '*')
        {
            var end = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return end < 0 ? t.Length : end + 1;
        }

        var verbatim = (c == '@' && i + 1 < t.Length && t[i + 1] == '"') || (c == '$' && i + 2 < t.Length && t[i + 1] == '@' && t[i + 2] == '"') || (c == '@' && i + 2 < t.Length && t[i + 1] == '$' && t[i + 2] == '"');
        if (verbatim)
        {
            var j = t.IndexOf('"', i) + 1;
            while (j < t.Length)
            {
                if (t[j] == '"')
                {
                    if (j + 1 < t.Length && t[j + 1] == '"') { j += 2; continue; }
                    return j;
                }

                j++;
            }

            return t.Length;
        }

        if (c == '"' || (c == '$' && i + 1 < t.Length && t[i + 1] == '"'))
        {
            var j = c == '$' ? i + 2 : i + 1;
            while (j < t.Length && t[j] != '"' && t[j] != '\n')
                j += t[j] == '\\' ? 2 : 1;
            return Math.Min(j, t.Length - 1);
        }

        if (c == '\'')
        {
            var j = i + 1;
            while (j < t.Length && t[j] != '\'' && t[j] != '\n')
                j += t[j] == '\\' ? 2 : 1;
            return Math.Min(j, t.Length - 1);
        }

        return i;
    }

    /// <summary>Top-level statements of a method body; each keeps the comments and blank lines in front of it and its own line break.</summary>
    private static List<string> SplitStatements(string body)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i < body.Length; i++)
        {
            i = SkipLiteral(body, i);
            if (i >= body.Length)
                break;
            var c = body[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']') depth--;
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && EndsBlockStatement(body, i + 1))
                    Cut(i + 1);
            }
            else if (c == ';' && depth == 0)
                Cut(i + 1);
        }

        if (start < body.Length && body[start..].Trim().Length > 0)
            result.Add(body[start..]);
        return result;

        void Cut(int end)
        {
            var nl = body.IndexOf('\n', end);
            var stop = nl < 0 || body[end..nl].Trim().Length > 0 ? end : nl + 1;
            result.Add(body[start..stop]);
            start = stop;
        }
    }

    private static bool EndsBlockStatement(string body, int after)
    {
        var rest = body[after..].TrimStart();
        if (rest.Length == 0)
            return true;
        if (rest[0] is ';' or ')' or '.' or ',' or '?' or ':')
            return false;
        return !Regex.IsMatch(rest, @"^(else|catch|finally|while)\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private static string StripComments(string statement)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < statement.Length; i++)
        {
            var end = SkipLiteral(statement, i);
            if (end != i && statement[i] == '/')
            {
                i = end;
                continue;
            }

            sb.Append(statement[i]);
        }

        return sb.ToString();
    }

    private sealed record Helper(string Name, int Start, int End, string Text);

    /// <summary>The private static methods declared in the class of <paramref name="text"/>, with the comment and attribute lines in front of them and their trailing line break.</summary>
    private static List<Helper> PrivateHelpers(string text)
    {
        var result = new List<Helper>();
        foreach (Match m in PrivateStaticMethod().Matches(text))
        {
            var start = text.LastIndexOf('\n', m.Index) + 1;
            while (start > 0)
            {
                var previousStart = text.LastIndexOf('\n', start - 2) + 1;
                var line = text[previousStart..(start - 1)].TrimStart();
                if (!line.StartsWith("///", StringComparison.Ordinal) && !line.StartsWith('['))
                    break;
                start = previousStart;
            }

            var end = EndOfMember(text, m.Index + m.Length);
            if (end < 0)
                continue;
            var nl = text.IndexOf('\n', end);
            end = nl < 0 ? text.Length : nl + 1;
            // one blank line in front goes with the method
            var blank = start;
            if (start >= 2)
            {
                var previousStart = text.LastIndexOf('\n', start - 2) + 1;
                if (text[previousStart..(start - 1)].Trim().Length == 0)
                    blank = previousStart;
            }

            result.Add(new Helper(m.Groups["name"].Value, blank, end, text[start..end]));
        }

        return result;
    }

    /// <summary>The end of a method or expression-bodied member whose parameter list opens at <paramref name="from"/> - 1: the closing brace of the block, or the semicolon of the expression.</summary>
    private static int EndOfMember(string text, int from)
    {
        var depth = 1;
        var i = from;
        for (; i < text.Length && depth > 0; i++)
        {
            i = SkipLiteral(text, i);
            if (i >= text.Length)
                return -1;
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
        }

        var expression = false;
        for (depth = 0; i < text.Length; i++)
        {
            i = SkipLiteral(text, i);
            if (i >= text.Length)
                return -1;
            var c = text[i];
            if (c == '=' && i + 1 < text.Length && text[i + 1] == '>' && depth == 0)
                expression = true;
            else if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']') depth--;
            else if (c == '}' && --depth == 0 && !expression)
                return i + 1;
            else if (c == ';' && depth == 0)
                return i + 1;
        }

        return -1;
    }

    /// <summary>The helpers that only the moved statements need: used by a moved statement or by a helper that moves, and by no kept statement or kept helper.</summary>
    private static List<Helper> HelpersThatMove(List<Helper> helpers, List<string> moved, List<string> kept, List<string> manual)
    {
        if (helpers.Count == 0)
            return new List<Helper>();
        var names = helpers.Select(h => h.Name).ToHashSet(StringComparer.Ordinal);
        var wanted = new HashSet<string>(moved.SelectMany(s => Word().Matches(StripComments(s)).Select(w => w.Value)).Where(names.Contains), StringComparer.Ordinal);
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var h in helpers.Where(h => wanted.Contains(h.Name)))
                foreach (var w in Word().Matches(StripComments(h.Text)).Select(w => w.Value).Where(w => names.Contains(w) && wanted.Add(w)))
                    grew = true;
        }

        var result = new List<Helper>();
        foreach (var h in helpers.Where(h => wanted.Contains(h.Name)))
        {
            var usedByKept = kept.Any(s => UsesWord(s, h.Name))
                             || helpers.Any(o => !wanted.Contains(o.Name) && UsesWord(o.Text, h.Name));
            if (usedByKept)
                manual.Add($"helper '{h.Name}' is used by a moved statement and by code that stays: make it shared or copy it.");
            else
                result.Add(h);
        }

        return result;
    }

    [GeneratedRegex(@"(?m)^[ \t]*private\s+static\s+(?:async\s+)?[^\n(=;{]+?\s(?<name>\w+)\s*(?:<[^>]*>)?\s*\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PrivateStaticMethod();

    [GeneratedRegex(@"\b(?:var|[A-Z]\w*(?:<[^>]*>)?)\s+(?<name>[a-z_]\w*)\s*=", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LocalDeclaration();
}
