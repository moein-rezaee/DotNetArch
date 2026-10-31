using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>add_adapter</c>: creates the empty adapter project <c>&lt;App&gt;.Infrastructure.&lt;System&gt;</c> of a module (the destination <c>move_code</c> needs) with one registration
/// method <c>Add&lt;System&gt;Adapter</c>, a settings schema, the package metadata the profile pattern names, and the solution entry. Nothing in it belongs to an organisation or to a
/// vendor: the name comes from the module's own project graph.
/// </summary>
internal static partial class AdapterOperations
{
    public static OperationDefinition AddAdapter { get; } = new(
        "add_adapter",
        "Create the adapter project <App>.Infrastructure.<System> of a module: a project that references the module's Domain only, one registration method Add<System>Adapter, an empty settings.schema.json that the package carries, the package id and version lines from the profile's adapter_package pattern, and the solution entry. It is the destination for move_code (the client code) and split_registration (its registration). A second run changes nothing. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            PathParam(),
            new OperationParameter("system", "Name of the system on the other side (the part after <App>.Infrastructure.), for example InvoiceRest.", Required: true),
        },
        RunAddAdapter);

    private static readonly string[] DependencyInjectionPackages = { "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Http" };
    private static readonly string[] ConfigurationPackages = { "Microsoft.Extensions.Configuration.Abstractions", "Microsoft.Extensions.Configuration", "Microsoft.Extensions.Configuration.Binder" };

    private static OperationResult RunAddAdapter(OperationRequest request)
    {
        var root = Root(request);
        var ctx = Context(root);
        var system = request.Get("system") ?? throw new ArgumentException("Missing required value 'system'.");
        if (!Regex.IsMatch(system, @"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new ArgumentException("The system name is letters and digits only (for example InvoiceRest).");
        var host = AdapterChecks.Host(ctx) ?? throw new ArgumentException("The module has no shared <App>.Infrastructure project to take the application name from.");
        var app = host.Name[..^".Infrastructure".Length];
        var name = $"{app}.Infrastructure.{system}";
        if (ctx.Source.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return new OperationResult(true, $"add_adapter: {name} already exists (0 changes).", Plan: Array.Empty<PlannedChange>());
        var domain = ctx.OfLayer(ProjectLayer.Domain).FirstOrDefault() ?? throw new ArgumentException("The module has no Domain project for the adapter to reference.");

        var manual = new List<string>();
        var module = app.EndsWith("Service", StringComparison.Ordinal) ? app[..^"Service".Length] : app;
        var framework = XDocument.Load(Path.Combine(root, host.File)).Descendants("TargetFramework").FirstOrDefault()?.Value ?? "net9.0";
        var dir = $"{host.Dir[..host.Dir.LastIndexOf('/')]}/{name}";
        var csprojPath = $"{dir}/{name}.csproj";

        var packaging = string.Empty;
        var pattern = ctx.Profile?.AdapterPackage;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            manual.Add("the profile has no adapter_package pattern, so the project is not a package yet: fix --rules=DA-AD05 writes it once the pattern is set");
        }
        else
        {
            var id = pattern.Replace("{Module}", module, StringComparison.Ordinal).Replace("{System}", system, StringComparison.Ordinal);
            packaging = $"    <IsPackable>true</IsPackable>\n    <PackageId>{id}</PackageId>\n    <Description>The {system} adapter of the {module} module.</Description>\n"
                        + "    <Version>1.0.0</Version>\n    <Version Condition=\"'$(AdapterVersion)' != ''\">$(AdapterVersion)</Version>\n";
        }

        var packages = new StringBuilder();
        var central = ctx.Has("Directory.Packages.props") ? ctx.Read("Directory.Packages.props") : null;
        var hostText = ctx.Read(host.File);
        foreach (var candidates in new[] { DependencyInjectionPackages, ConfigurationPackages })
        {
            var chosen = candidates.FirstOrDefault(c => central != null
                ? central.Contains($"PackageVersion Include=\"{c}\"", StringComparison.OrdinalIgnoreCase)
                : hostText.Contains($"PackageReference Include=\"{c}\"", StringComparison.OrdinalIgnoreCase));
            if (chosen == null)
                manual.Add($"add a reference to one of {string.Join(", ", candidates)} (the registration method needs it)");
            else if (central != null)
                packages.Append($"    <PackageReference Include=\"{chosen}\" />\n");
            else
                packages.Append(Regex.Match(hostText, $"<PackageReference\\s+Include=\"{Regex.Escape(chosen)}\"[^>]*/>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Value is { Length: > 0 } line ? $"    {line}\n" : string.Empty);
        }

        var domainRef = Path.GetRelativePath(dir, domain.File).Replace('/', '\\');
        var csproj = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>{framework}</TargetFramework>\n    <ImplicitUsings>enable</ImplicitUsings>\n    <Nullable>enable</Nullable>\n{packaging}  </PropertyGroup>\n\n"
                     + $"  <ItemGroup>\n    <ProjectReference Include=\"{domainRef}\" />\n  </ItemGroup>\n\n  <ItemGroup>\n{packages}  </ItemGroup>\n\n"
                     + "  <ItemGroup>\n    <None Include=\"settings.schema.json\" Pack=\"true\" PackagePath=\"schemas/\" />\n  </ItemGroup>\n\n</Project>\n";
        var code = $"using Microsoft.Extensions.Configuration;\nusing Microsoft.Extensions.DependencyInjection;\n\nnamespace {name};\n\npublic static class DependencyInjection\n{{\n"
                   + $"    /// <summary>Registers everything the {system} adapter owns. A composition project selects it; the shared Infrastructure registration does not know it.</summary>\n"
                   + $"    public static IServiceCollection Add{system}Adapter(this IServiceCollection services, IConfiguration configuration)\n    {{\n        return services;\n    }}\n}}\n";
        var schema = $"{{\n  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n  \"title\": \"{name} settings\",\n  \"description\": \"Run describe_settings after the code has moved in.\",\n  \"type\": \"object\",\n  \"properties\": {{}}\n}}\n";

        var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        var plan = new List<FixAction>
        {
            new(new PlannedChange(csprojPath, "create", $"adapter project {name}", "add_adapter"), csproj, solution == null ? null : () => RegisterInSolution(root, solution, csprojPath)),
            new(new PlannedChange($"{dir}/DependencyInjection.cs", "create", $"Add{system}Adapter", "add_adapter"), code),
            new(new PlannedChange($"{dir}/settings.schema.json", "create", "empty settings schema the package carries", "add_adapter"), schema),
        };
        if (solution == null)
            manual.Add("no .sln at the repository root: add the project to the solution yourself");
        return Planned("add_adapter", root, plan, request.Apply, manual);
    }
}
