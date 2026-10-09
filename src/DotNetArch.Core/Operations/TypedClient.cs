using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// The typed HTTP client of a service (<c>HttpApi.Client</c>): one interface and one class per controller, generated from the controller routes and from
/// the request and response types that live in Application.Contracts / Domain.Shared. It is new code (not a move), uses only the base class library
/// (<c>HttpClient</c> and <c>System.Net.Http.Json</c>), and an action it cannot express is listed as manual instead of being guessed.
/// </summary>
internal static partial class TypedClient
{
    private static readonly HashSet<string> Primitives = new(StringComparer.Ordinal)
    {
        "string", "int", "long", "short", "byte", "bool", "decimal", "double", "float", "Guid", "DateTime", "DateTimeOffset", "TimeSpan", "object",
    };

    private static readonly HashSet<string> Wrappers = new(StringComparer.Ordinal)
    {
        "IReadOnlyCollection", "IReadOnlyList", "IEnumerable", "List", "ICollection", "Dictionary", "IDictionary", "IReadOnlyDictionary", "Nullable", "Task", "ActionResult",
    };

    private sealed record Parameter(string Type, string Name, string Source);

    private sealed record ClientAction(string Name, string Verb, string Url, IReadOnlyList<Parameter> Parameters, string? Returns, bool Raw = false);

    public static IReadOnlyList<FixAction> Plan(string root, RepoContext ctx, out IReadOnlyList<string> manual)
    {
        var notes = new List<string>();
        manual = notes;
        var contracts = ctx.OfLayer(ProjectLayer.ApplicationContracts).FirstOrDefault();
        var domain = ctx.OfLayer(ProjectLayer.Domain).FirstOrDefault();
        if (domain == null)
            return Array.Empty<FixAction>();

        var controllers = ctx.Projects
            .Where(p => !p.IsTest && p.Layer is ProjectLayer.HttpApi or ProjectLayer.Api)
            .SelectMany(p => ctx.Files.Where(f => f.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase)))
            .Where(f => ControllerBase().IsMatch(ctx.Read(f)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        if (controllers.Count == 0)
            return Array.Empty<FixAction>();
        if (contracts == null)
        {
            notes.Add("DA-A08  the typed client needs Application.Contracts for its request and response types: run DA-A02 first");
            return Array.Empty<FixAction>();
        }

        var known = KnownTypes(ctx);
        var transport = ctx.Profile?.Client is { Transport: "rest-client", Interface.Length: > 0 } client ? client : null;
        var prefix = domain.Name[..^".Domain".Length];
        var existing = ctx.OfLayer(ProjectLayer.HttpApiClient).FirstOrDefault();
        var folder = domain.Dir.Contains('/') ? domain.Dir[..domain.Dir.LastIndexOf('/')] : string.Empty;
        var name = $"{prefix}.HttpApi.Client";
        var dir = existing?.Dir ?? (folder.Length == 0 ? name : $"{folder}/{name}");
        var ns = name;

        var actions = new List<FixAction>();
        var generated = 0;
        foreach (var file in controllers)
        {
            var (clientName, methods) = Parse(ctx.Read(file), file, known, notes, transport != null);
            if (methods.Count == 0)
                continue;
            var path = $"{dir}/{clientName}.cs";
            if (ctx.Has(path))
                continue;
            actions.Add(new FixAction(new PlannedChange(path, "create", $"typed client for {Path.GetFileNameWithoutExtension(file)} ({methods.Count} action(s))", "DA-A08"), Render(ns, clientName, methods, known, transport)));
            generated++;
        }

        if (generated == 0)
            return actions;

        if (!ctx.Has($"{dir}/ApiRoute.cs"))
            actions.Add(new FixAction(new PlannedChange($"{dir}/ApiRoute.cs", "create", "URL helpers of the typed client", "DA-A08"), RenderRoute(ns, transport != null)));

        if (existing == null)
        {
            var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
            var csproj = $"{dir}/{name}.csproj";
            var framework = XDocument.Load(Path.Combine(root, domain.File)).Descendants("TargetFramework").FirstOrDefault()?.Value ?? "net8.0";
            var relative = Path.GetRelativePath(dir, $"{contracts.Dir}/{contracts.Name}.csproj").Replace('/', '\\');
            actions.Add(new FixAction(new PlannedChange(csproj, "create", "typed client project (references Application.Contracts only)", "DA-A08"),
                $"<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>{framework}</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n\n  <ItemGroup>\n    <ProjectReference Include=\"{relative}\" />\n  </ItemGroup>\n{(transport == null ? string.Empty : $"\n  <ItemGroup>\n    <PackageReference Include=\"{transport.Package}\" />\n  </ItemGroup>\n")}\n</Project>\n",
                solution == null ? null : () => Register(root, solution, csproj)));
        }

        return actions;
    }

    /// <summary>True when the HttpApi layer holds controllers and no typed client project exists yet (doctor DA-A08).</summary>
    public static bool IsMissing(RepoContext ctx) =>
        ctx.OfLayer(ProjectLayer.HttpApi).Any()
        && !ctx.OfLayer(ProjectLayer.HttpApiClient).Any()
        && ctx.OfLayer(ProjectLayer.HttpApi).Any(p => ctx.Files.Any(f => f.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase) && ControllerBase().IsMatch(ctx.Read(f))));

    private static void Register(string root, string solution, string project)
    {
        var result = Hosting.ToolHost.Runner.Run(new Hosting.ProcessSpec("dotnet", new[] { "sln", solution, "add", project }, root), showProgress: false);
        if (!result.Success)
            throw new InvalidOperationException($"dotnet sln add failed: {result.Output}");
    }

    /// <summary>Types the client may use: everything declared in Application.Contracts and Domain.Shared (name to namespace).</summary>
    private static Dictionary<string, string> KnownTypes(RepoContext ctx)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in ctx.Projects.Where(p => !p.IsTest && p.Layer is ProjectLayer.ApplicationContracts or ProjectLayer.DomainShared))
        {
            foreach (var file in ctx.Files.Where(f => f.StartsWith(project.Dir + "/", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var text = ctx.Read(file);
                var space = Namespace().Match(text);
                if (!space.Success)
                    continue;
                foreach (Match declaration in Declaration().Matches(text))
                    result.TryAdd(declaration.Groups["name"].Value, space.Groups[1].Value);
            }
        }

        return result;
    }

    private static (string ClientName, List<ClientAction> Actions) Parse(string raw, string file, Dictionary<string, string> known, List<string> notes, bool stringTransport)
    {
        var text = LineComment().Replace(BlockComment().Replace(raw, " "), string.Empty);
        var controller = ControllerClass().Match(text);
        var actions = new List<ClientAction>();
        if (!controller.Success)
            return (string.Empty, actions);
        var controllerName = controller.Groups["name"].Value;
        var token = controllerName[..^"Controller".Length];
        var route = RouteOnClass().Match(text[..controller.Index]);
        var baseRoute = route.Success ? route.Groups["t"].Value.Replace("[controller]", token, StringComparison.OrdinalIgnoreCase) : token;

        foreach (Match method in ActionMethod().Matches(text[controller.Index..]))
        {
            var attributes = method.Groups["attrs"].Value;
            var verb = HttpVerb().Match(attributes);
            if (!verb.Success)
                continue;
            var methodName = method.Groups["name"].Value;
            var template = verb.Groups["tpl"].Success ? verb.Groups["tpl"].Value : string.Empty;
            var url = string.Join('/', new[] { baseRoute.Trim('/'), RouteConstraint().Replace(template, "{$1}").Trim('/') }.Where(s => s.Length > 0));
            var label = $"{controllerName}.{methodName}";

            var parameters = new List<Parameter>();
            string? problem = null;
            var hasBody = false;
            foreach (var raw1 in SplitParameters(method.Groups["params"].Value))
            {
                var attrs = string.Concat(ParameterAttribute().Matches(raw1).Select(m => m.Value));
                var cleaned = DefaultValue().Replace(ParameterAttribute().Replace(raw1, string.Empty), string.Empty).Trim();
                var cut = cleaned.LastIndexOf(' ');
                if (cut < 0)
                    continue;
                var type = cleaned[..cut].Trim();
                var parameterName = cleaned[(cut + 1)..].Trim();
                if (type == "CancellationToken")
                    continue;
                string source;
                if (attrs.Contains("FromBody", StringComparison.Ordinal))
                    source = "body";
                else if (attrs.Contains("FromQuery", StringComparison.Ordinal))
                    source = "query";
                else if (attrs.Contains("FromRoute", StringComparison.Ordinal) || template.Contains("{" + parameterName, StringComparison.Ordinal))
                    source = "route";
                else if (attrs.Contains("From", StringComparison.Ordinal))
                    source = "unsupported";
                else if (IsSimple(type, known) || IsSimpleCollection(type, known))
                    source = "query";
                else
                    source = verb.Groups["verb"].Value is "Post" or "Put" or "Patch" && !hasBody ? "body" : "unsupported";

                if (source == "unsupported")
                    problem = $"parameter {parameterName} comes from a place the client cannot express";
                else if (source == "body")
                    hasBody = true;
                else if (!IsSimple(type, known) && !(source == "query" && IsSimpleCollection(type, known)))
                    problem = $"{source} parameter {parameterName} has the non-simple type {type}";
                if (!TypeAllowed(type, known, out var blocker))
                    problem = $"type {blocker} is not in Application.Contracts or Domain.Shared";
                parameters.Add(new Parameter(type, parameterName, source));
            }

            var returns = ProducedType().Matches(attributes).Select(m => (Code: m.Groups["code"].Value, Type: m.Groups["t"].Value.Trim())).FirstOrDefault(r => r.Code.StartsWith("Status20", StringComparison.Ordinal));
            if (returns.Type != null && !TypeAllowed(returns.Type, known, out var returnBlocker))
                problem = $"response type {returnBlocker} is not in Application.Contracts or Domain.Shared";
            var rawBody = returns.Type == null && verb.Groups["verb"].Value == "Get";
            if (rawBody && stringTransport)
                problem = "the response is raw bytes and the configured REST client returns strings";
            if (problem != null)
            {
                notes.Add($"DA-A08  {label} skipped: {problem}");
                continue;
            }

            actions.Add(new ClientAction(methodName, verb.Groups["verb"].Value, url, parameters, rawBody ? "byte[]" : returns.Type, rawBody));
        }

        return (token + "Client", actions);
    }

    private static bool IsSimple(string type, Dictionary<string, string> known)
    {
        var core = type.TrimEnd('?');
        return Primitives.Contains(core) || (known.ContainsKey(core) && !core.EndsWith("Dto", StringComparison.Ordinal) && !core.EndsWith("Request", StringComparison.Ordinal) && !core.EndsWith("Response", StringComparison.Ordinal) && !core.EndsWith("Command", StringComparison.Ordinal) && !core.EndsWith("Query", StringComparison.Ordinal));
    }

    private static bool IsSimpleCollection(string type, Dictionary<string, string> known)
    {
        var match = CollectionOf().Match(type.TrimEnd('?'));
        return match.Success && IsSimple(match.Groups["item"].Value, known);
    }

    private static bool TypeAllowed(string type, Dictionary<string, string> known, out string blocker)
    {
        foreach (Match identifier in Identifier().Matches(type))
        {
            var word = identifier.Value;
            if (Primitives.Contains(word) || Wrappers.Contains(word) || known.ContainsKey(word))
                continue;
            blocker = word;
            return false;
        }

        blocker = string.Empty;
        return true;
    }

    private static string Render(string ns, string clientName, List<ClientAction> actions, Dictionary<string, string> known, NetArch.ClientTransport? transport)
    {
        var usedTypes = actions.SelectMany(a => a.Parameters.Select(p => p.Type).Append(a.Returns ?? string.Empty)).SelectMany(t => Identifier().Matches(t).Select(m => m.Value));
        var usings = usedTypes.Where(known.ContainsKey).Select(t => known[t]).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var header = new StringBuilder(transport == null ? "using System.Net.Http.Json;\n" : "");
        if (transport != null)
            usings.Add(transport.Namespace);
        usings = usings.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        foreach (var u in usings)
            header.Append("using ").Append(u).Append(";\n");
        var interfaceBody = new StringBuilder();
        var classBody = new StringBuilder();
        foreach (var action in actions)
        {
            var methodName = action.Name.EndsWith("Async", StringComparison.Ordinal) ? action.Name : action.Name + "Async";
            var parameterList = string.Join(", ", action.Parameters.Select(p => $"{p.Type} {p.Name}").Append("CancellationToken cancellationToken = default"));
            var returnType = action.Returns == null ? "Task" : $"Task<{NullableOf(action.Returns)}>";
            interfaceBody.Append("    ").Append(returnType).Append(' ').Append(methodName).Append('(').Append(parameterList).Append(");\n");
            classBody.Append(RenderMethod(action, methodName, parameterList, returnType, transport != null));
        }

        var fieldAndConstructor = transport == null
            ? $"private readonly HttpClient _http;\n\n    public {clientName}(HttpClient http)\n    {{\n        _http = http ?? throw new ArgumentNullException(nameof(http));\n    }}"
            : $"private readonly {transport.Interface} _rest;\n\n    public {clientName}({transport.Interface} rest)\n    {{\n        _rest = rest ?? throw new ArgumentNullException(nameof(rest));\n    }}";
        return $"{header}\nnamespace {ns};\n\n/// <summary>Typed client for the {clientName[..^"Client".Length]} endpoints (generated from the controller routes).</summary>\npublic interface I{clientName}\n{{\n{interfaceBody}}}\n\n/// <inheritdoc />\npublic sealed class {clientName} : I{clientName}\n{{\n    {fieldAndConstructor}\n\n{classBody.ToString().TrimEnd('\n')}\n}}\n";
    }

    private static string NullableOf(string type) =>
        type.EndsWith('?') || Primitives.Contains(type) && type is "int" or "long" or "short" or "byte" or "bool" or "decimal" or "double" or "float" or "Guid" or "DateTime" or "DateTimeOffset" or "TimeSpan" ? type : type + "?";

    private static string RenderMethod(ClientAction action, string methodName, string parameterList, string returnType, bool rest)
    {
        var sb = new StringBuilder();
        sb.Append("    public async ").Append(returnType).Append(' ').Append(methodName).Append('(').Append(parameterList).Append(")\n    {\n");
        var url = RouteParameter().Replace($"\"{action.Url}\"", m =>
        {
            var parameter = action.Parameters.FirstOrDefault(p => p.Source == "route" && p.Name == m.Groups[1].Value);
            return parameter == null ? m.Value : "\" + ApiRoute.Segment(" + parameter.Name + ") + \"";
        });
        url = url.Replace(" + \"\"", string.Empty, StringComparison.Ordinal).Replace("\"\" + ", string.Empty, StringComparison.Ordinal);
        var query = action.Parameters.Where(p => p.Source == "query").ToList();
        sb.Append("        var url = ").Append(url).Append(";\n");
        if (query.Count > 0)
            sb.Append("        url = ApiRoute.WithQuery(url, ").Append(string.Join(", ", query.Select(p => $"(\"{p.Name}\", {p.Name})"))).Append(");\n");
        var body = action.Parameters.FirstOrDefault(p => p.Source == "body");
        var returnsValue = action.Returns != null;
        if (rest)
        {
            // a string-returning REST client: the query string travels inside the path, so repeated keys survive
            var call = action.Verb + "Async";
            var arguments = action.Verb is "Get" or "Delete" ? "url, null, null, cancellationToken" : $"url, {(body != null ? body.Name : "null")}, null, null, cancellationToken";
            if (returnsValue)
                sb.Append("        var json = await _rest.").Append(call).Append('(').Append(arguments).Append(").ConfigureAwait(false);\n        return ApiJson.Read<").Append(action.Returns).Append(">(json);\n");
            else
                sb.Append("        await _rest.").Append(call).Append('(').Append(arguments).Append(").ConfigureAwait(false);\n");
            sb.Append("    }\n\n");
            return sb.ToString();
        }

        switch (action.Verb)
        {
            case "Get" when action.Raw:
                sb.Append("        return await _http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);\n");
                break;
            case "Get":
                sb.Append("        return await _http.GetFromJsonAsync<").Append(action.Returns).Append(">(url, cancellationToken).ConfigureAwait(false);\n");
                break;
            case "Delete":
                sb.Append("        using var response = await _http.DeleteAsync(url, cancellationToken).ConfigureAwait(false);\n        response.EnsureSuccessStatusCode();\n");
                if (returnsValue)
                    sb.Append("        return await response.Content.ReadFromJsonAsync<").Append(action.Returns).Append(">(cancellationToken: cancellationToken).ConfigureAwait(false);\n");
                break;
            default:
                var call = action.Verb switch { "Put" => "PutAsJsonAsync", "Patch" => "PatchAsJsonAsync", _ => "PostAsJsonAsync" };
                if (body != null)
                    sb.Append("        using var response = await _http.").Append(call).Append("(url, ").Append(body.Name).Append(", cancellationToken).ConfigureAwait(false);\n");
                else
                    sb.Append("        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.").Append(action.Verb).Append(", url), cancellationToken).ConfigureAwait(false);\n");
                sb.Append("        response.EnsureSuccessStatusCode();\n");
                if (returnsValue)
                    sb.Append("        return await response.Content.ReadFromJsonAsync<").Append(action.Returns).Append(">(cancellationToken: cancellationToken).ConfigureAwait(false);\n");
                break;
        }

        sb.Append("    }\n\n");
        return sb.ToString();
    }

    private static string RenderRoute(string ns, bool rest) =>
        $"using System.Globalization;\n\nnamespace {ns};\n\n/// <summary>Builds the URLs of the typed clients: escaped route segments and a query string without null values.</summary>\ninternal static class ApiRoute\n{{\n    public static string Segment(object? value) => Uri.EscapeDataString(Format(value));\n\n    public static string WithQuery(string url, params (string Name, object? Value)[] query)\n    {{\n        var parts = query\n            .Where(item => item.Value != null)\n            .SelectMany(item => item.Value is System.Collections.IEnumerable list and not string ? list.Cast<object?>().Select(v => (item.Name, Value: v)) : new[] {{ (item.Name, Value: item.Value) }})\n            .Where(item => item.Value != null)\n            .Select(item => Uri.EscapeDataString(item.Name) + \"=\" + Uri.EscapeDataString(Format(item.Value)))\n            .ToList();\n        return parts.Count == 0 ? url : url + \"?\" + string.Join('&', parts);\n    }}\n\n    private static string Format(object? value) => value switch\n    {{\n        null => string.Empty,\n        bool flag => flag ? \"true\" : \"false\",\n        DateTime moment => moment.ToString(\"O\", CultureInfo.InvariantCulture),\n        DateTimeOffset moment => moment.ToString(\"O\", CultureInfo.InvariantCulture),\n        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),\n        _ => value.ToString() ?? string.Empty,\n    }};\n}}\n" + (rest ? $"\n/// <summary>Reads the JSON text a string-returning REST client delivers.</summary>\ninternal static class ApiJson\n{{\n    private static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);\n\n    public static T? Read<T>(string json) => string.IsNullOrWhiteSpace(json) ? default : System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);\n}}\n" : string.Empty);

    private static List<string> SplitParameters(string arguments)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new StringBuilder();
        foreach (var c in arguments)
        {
            if (c is '<' or '(' or '[')
                depth++;
            else if (c is '>' or ')' or ']')
                depth--;
            if (c == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.ToString().Trim().Length > 0)
            parts.Add(current.ToString().Trim());
        return parts;
    }

    [GeneratedRegex(@":\s*(?:Microsoft\.AspNetCore\.Mvc\.)?(?:ControllerBase|Controller)\b|\[ApiController\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ControllerBase();

    [GeneratedRegex(@"class\s+(?<name>\w+Controller)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ControllerClass();

    [GeneratedRegex(@"\[Route\(\s*""(?<t>[^""]*)""\s*\)\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RouteOnClass();

    [GeneratedRegex(@"(?<attrs>(?:[ \t]*\[(?:[^\[\]]|\[[^\]]*\])*\][ \t]*\r?\n)+)[ \t]*public\s+(?:async\s+)?(?<ret>[\w<>\[\]?,. ]+?)\s+(?<name>\w+)\s*\((?<params>(?:[^()]|\([^()]*\))*)\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 3000)]
    private static partial Regex ActionMethod();

    [GeneratedRegex(@"\[Http(?<verb>Get|Post|Put|Delete|Patch)(?:\(\s*""(?<tpl>[^""]*)""\s*\))?\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HttpVerb();

    [GeneratedRegex(@"ProducesResponseType\(\s*typeof\((?<t>[^()]+)\)\s*,\s*(?:StatusCodes\.)?(?<code>\w+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ProducedType();

    [GeneratedRegex(@"\{(\w+):[^}]+\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RouteConstraint();

    [GeneratedRegex(@"\[[^\]]+\]\s*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ParameterAttribute();

    [GeneratedRegex(@"\s*=\s*[^,]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DefaultValue();

    [GeneratedRegex(@"^(?:List|IEnumerable|IReadOnlyCollection|IReadOnlyList|ICollection)<(?<item>[^<>]+)>$|^(?<item>[^<>\[\]]+)\[\]$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CollectionOf();

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RouteParameter();

    [GeneratedRegex(@"[A-Za-z_]\w*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\b(?<kind>class|record\s+struct|record\s+class|record|struct|interface|enum)\s+(?<name>[A-Za-z_]\w*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Declaration();

    [GeneratedRegex(@"namespace\s+([\w.]+)\s*[;{]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Namespace();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"(?m)(?<!:)//[^\n]*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LineComment();
}
