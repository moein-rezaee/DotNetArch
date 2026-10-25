using System.Text;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// Adds a CLI host (<c>src/&lt;App&gt;.Cli</c>) to a layout-v2/v3 solution. It is a peer of the MCP host: both send the same MediatR requests as the API
/// controllers straight into the Application layer, so a command, a tool and an endpoint are the same use case. Entities, actions and typed use cases
/// get their commands automatically; a parity test keeps the tool set and the command set equal when both hosts exist.
/// </summary>
public static class CliV2Generator
{
    public static bool Add(SolutionConfig config)
    {
        if (!config.IsV2)
        {
            ToolHost.Error("A CLI host needs a layout v2 solution.");
            return false;
        }

        var app = config.SolutionName;
        var host = $"src/{app}.Cli";
        if (Directory.Exists(Path.Combine(config.SolutionPath, host)))
        {
            ToolHost.Error("The solution already has a CLI host.", host);
            return false;
        }

        EnsurePackageVersion(config, "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Hosting.Abstractions");

        var tokens = Tokens(config);
        var writer = new FileWriter(config.SolutionPath);
        var files = new (string Template, string Output)[]
        {
            ("Cli.csproj", $"{host}/{app}.Cli.csproj"),
            ("Program.cs", $"{host}/Program.cs"),
            ("CliCommand.cs", $"{host}/CliCommand.cs"),
            ("CliContext.cs", $"{host}/CliContext.cs"),
            ("CliApp.cs", $"{host}/CliApp.cs"),
            ("Configuration/CliHostExtensions.cs", $"{host}/Configuration/CliHostExtensions.cs"),
            ("appsettings.json", $"{host}/appsettings.json"),
            ("appsettings.Development.json", $"{host}/appsettings.Development.json"),
            ("appsettings.example.json", $"{host}/appsettings.example.json"),
            (".env.example", $"{host}/.env.example"),
            (".env", $"{host}/.env"),
            ("Properties/launchSettings.json", $"{host}/Properties/launchSettings.json"),
        };
        foreach (var (template, output) in files)
            writer.Write(output, TemplateRenderer.RenderTemplate($"V2/cli/{template}.tpl", tokens));

        var projects = new List<string> { $"{host}/{app}.Cli.csproj" };
        var withTests = Directory.Exists(Path.Combine(config.SolutionPath, config.TestsRoot, $"{app}.Domain.Tests"));
        if (withTests)
        {
            var tests = $"{config.TestsRoot}/{app}.Cli.Tests";
            writer.Write($"{tests}/{app}.Cli.Tests.csproj", TemplateRenderer.RenderTemplate("V2/cli-tests/Cli.Tests.csproj.tpl", tokens));
            writer.Write($"{tests}/Support/FakeUnitOfWork.cs", TemplateRenderer.RenderTemplate("V2/cli-tests/Support/FakeUnitOfWork.cs.tpl", tokens));
            writer.Write($"{tests}/Support/CliHarness.cs", TemplateRenderer.RenderTemplate("V2/cli-tests/Support/CliHarness.cs.tpl", tokens));
            writer.Write($"{tests}/CliBasicsTests.cs", TemplateRenderer.RenderTemplate("V2/cli-tests/CliBasicsTests.cs.tpl", tokens));
            projects.Add($"{tests}/{app}.Cli.Tests.csproj");
        }

        foreach (var project in projects)
            ToolHost.RunCommand($"dotnet sln add {project}", config.SolutionPath);

        config.CliEnabled = true;
        ConfigManager.Save(config.SolutionPath, config);

        foreach (var (entity, state) in config.Entities.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (state.HasCrud)
                AddEntityCommands(config, entity);
            foreach (var action in FindActions(config, entity))
                AddActionCommand(config, entity, action.Name, action.IsQuery);
        }

        foreach (var useCase in config.UseCases.Values.OrderBy(u => u.Entity, StringComparer.Ordinal).ThenBy(u => u.Action, StringComparer.Ordinal))
            AddTypedActionCommand(config, useCase);

        EnsureParity(config);
        ToolHost.Success($"CLI host added ({writer.Created.Count} files).", $"run it with: dotnet run --project {host} -- help");
        return true;
    }

    internal static void AddEntityCommands(SolutionConfig config, string entity)
    {
        var tokens = EntityTokens(config, entity);
        var plural = tokens["Plural"];
        var writer = new FileWriter(config.SolutionPath);
        writer.Write($"src/{config.SolutionName}.Cli/Commands/{plural}/{plural}Commands.cs", TemplateRenderer.RenderTemplate("V2/cli-crud/Commands.cs.tpl", tokens));
        if (Directory.Exists(Path.Combine(config.SolutionPath, config.TestsRoot, $"{config.SolutionName}.Cli.Tests")))
            writer.Write($"{config.TestsRoot}/{config.SolutionName}.Cli.Tests/Commands/{plural}CommandsTests.cs", TemplateRenderer.RenderTemplate("V2/cli-tests/CommandsTests.cs.tpl", tokens));

        ToolHost.Info($"CLI commands generated for {entity}.", $"{tokens["CliGroup"]} list|get|create|update|delete");
    }

    internal static void AddActionCommand(SolutionConfig config, string entity, string actionName, bool isQuery)
    {
        var tokens = EntityTokens(config, entity);
        tokens["ActionName"] = actionName;
        tokens["ActionRoute"] = Naming.ToKebabCase(actionName);
        tokens["RequestKind"] = isQuery ? "Query" : "Command";
        var plural = tokens["Plural"];
        new FileWriter(config.SolutionPath).Write(
            $"src/{config.SolutionName}.Cli/Commands/{plural}/{actionName}{entity}CliCommand.cs",
            TemplateRenderer.RenderTemplate("V2/cli-action/Classic.cs.tpl", tokens));
    }

    internal static void AddTypedActionCommand(SolutionConfig config, UseCaseState useCase)
    {
        var spec = TypedActionV2Generator.SpecOf(useCase);
        var tokens = TypedActionV2Generator.Tokens(config, useCase.Entity, useCase.Action, useCase.Method, spec, useCase.Anonymous, null);
        tokens["CliGroup"] = Naming.ToKebabCase(useCase.Entity);
        tokens["CliOptions"] = string.Join(",\n", spec.Input.Select(p =>
            $"        new(\"{Naming.ToKebabCase(p.Name)}\", \"{p.Type.Replace("\"", string.Empty)}\", {(p.IsOptional ? "false" : "true")}, \"The {p.Description}.\")"));
        var reads = new StringBuilder();
        foreach (var property in spec.Input)
        {
            var option = Naming.ToKebabCase(property.Name);
            reads.AppendLine(property.IsOptional
                ? $"        var {property.ParameterName} = context.Optional<{property.Type}>(\"{option}\");"
                : $"        var {property.ParameterName} = context.Require<{property.Type}>(\"{option}\");");
        }

        tokens["CliReads"] = reads.ToString().TrimEnd('\n', '\r');
        var hasResult = spec.Output.Kind != UseCaseOutputKind.None;
        var request = $"new {useCase.Action}{useCase.Entity}{tokens["RequestKind"]}({spec.NamedArguments()})";
        tokens["CliRunSignature"] = hasResult
            ? "Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken)"
            : "async Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken)";
        tokens["CliSend"] = hasResult
            ? $"return Box(sender.Send({request}, cancellationToken));"
            : $"await sender.Send({request}, cancellationToken);\n        return \"Done.\";";
        var plural = tokens["Plural"];
        new FileWriter(config.SolutionPath).Write(
            $"src/{config.SolutionName}.Cli/Commands/{plural}/{useCase.Action}{useCase.Entity}CliCommand.cs",
            TemplateRenderer.RenderTemplate("V2/cli-action/Typed.cs.tpl", tokens));
    }

    /// <summary>When both peers exist, a test keeps the CLI commands and the MCP tools the same set.</summary>
    internal static void EnsureParity(SolutionConfig config)
    {
        var app = config.SolutionName;
        var tests = $"{config.TestsRoot}/{app}.Cli.Tests";
        if (!config.CliEnabled || !config.McpEnabled || !Directory.Exists(Path.Combine(config.SolutionPath, tests)))
            return;

        var project = $"{tests}/{app}.Cli.Tests.csproj";
        var writer = new FileWriter(config.SolutionPath);
        if (!writer.Write($"{tests}/ParityTests.cs", TemplateRenderer.RenderTemplate("V2/cli-tests/ParityTests.cs.tpl", new Dictionary<string, string> { ["App"] = app })))
            return;

        ToolHost.RunCommand($"dotnet add {project} reference src/{app}.Mcp/{app}.Mcp.csproj", config.SolutionPath);
    }

    private static IEnumerable<(string Name, bool IsQuery)> FindActions(SolutionConfig config, string entity)
    {
        var folder = Path.Combine(config.SolutionPath, "src", $"{config.SolutionName}.Application", "Features", Naming.Pluralize(entity), "Actions");
        if (!Directory.Exists(folder))
            yield break;

        foreach (var directory in Directory.GetDirectories(folder))
        {
            var name = Path.GetFileName(directory);
            if (!name.EndsWith(entity, StringComparison.Ordinal) || name.Length == entity.Length)
                continue;

            var action = name[..^entity.Length];
            if (config.UseCases.ContainsKey($"{entity}.{action}"))
                continue;
            yield return (action, File.Exists(Path.Combine(directory, $"{name}Query.cs")));
        }
    }

    private static void EnsurePackageVersion(SolutionConfig config, string package, string sameVersionAs)
    {
        var path = Path.Combine(config.SolutionPath, "Directory.Packages.props");
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        if (text.Contains($"Include=\"{package}\"", StringComparison.Ordinal))
            return;

        var match = System.Text.RegularExpressions.Regex.Match(text, $"<PackageVersion Include=\"{System.Text.RegularExpressions.Regex.Escape(sameVersionAs)}\" Version=\"([^\"]+)\" />");
        if (!match.Success)
            return;

        var line = $"    <PackageVersion Include=\"{package}\" Version=\"{match.Groups[1].Value}\" />\n";
        text = text.Replace(match.Value + "\n", match.Value + "\n" + line, StringComparison.Ordinal);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static Dictionary<string, string> EntityTokens(SolutionConfig config, string entity)
    {
        var plural = Naming.Pluralize(entity);
        return new Dictionary<string, string>
        {
            ["App"] = config.SolutionName,
            ["Entity"] = entity,
            ["Plural"] = plural,
            ["EntityLower"] = entity.ToLowerInvariant(),
            ["PluralLower"] = plural.ToLowerInvariant(),
            ["ToolName"] = Naming.ToKebabCase(entity).Replace('-', '_'),
            ["CliGroup"] = Naming.ToKebabCase(entity),
        };
    }

    private static Dictionary<string, string> Tokens(SolutionConfig config)
    {
        var tokens = OpsGenerator.Tokens(config);
        var databaseName = tokens["DatabaseName"];
        var example = DatabaseProviders.ExampleConnectionString(config.DatabaseProvider, databaseName);
        tokens["Provider"] = config.DatabaseProvider;
        tokens["CliName"] = Naming.ToKebabCase(config.SolutionName);
        tokens["ExampleConnectionString"] = example;
        tokens["DevConnectionString"] = example;
        return tokens;
    }
}
