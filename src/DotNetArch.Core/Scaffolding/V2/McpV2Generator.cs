using System.Security.Cryptography;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// Adds an MCP host (<c>src/&lt;App&gt;.Mcp</c>) to a layout-v2 solution. Its tools send the same MediatR requests as the API controllers,
/// so MCP and HTTP behave identically; new entities and actions get tools automatically once the host exists.
/// </summary>
public static class McpV2Generator
{
    public static bool Add(SolutionConfig config)
    {
        if (!config.IsV2)
        {
            ToolHost.Error("An MCP host needs a layout v2 solution.");
            return false;
        }

        var app = config.SolutionName;
        var host = $"src/{app}.Mcp";
        if (Directory.Exists(Path.Combine(config.SolutionPath, host)))
        {
            ToolHost.Error("The solution already has an MCP host.", host);
            return false;
        }

        var tokens = Tokens(config);
        var writer = new FileWriter(config.SolutionPath);
        var files = new (string Template, string Output)[]
        {
            ("Mcp.csproj", $"{host}/{app}.Mcp.csproj"),
            ("Program.cs", $"{host}/Program.cs"),
            ("Configuration/McpHostExtensions.cs", $"{host}/Configuration/McpHostExtensions.cs"),
            ("Authentication/McpAuthOptions.cs", $"{host}/Authentication/McpAuthOptions.cs"),
            ("Authentication/StaticTokenAuthenticationHandler.cs", $"{host}/Authentication/StaticTokenAuthenticationHandler.cs"),
            ("Tools/ToolRunner.cs", $"{host}/Tools/ToolRunner.cs"),
            ("appsettings.json", $"{host}/appsettings.json"),
            ("appsettings.Development.json", $"{host}/appsettings.Development.json"),
            ("appsettings.example.json", $"{host}/appsettings.example.json"),
            (".env.example", $"{host}/.env.example"),
            (".env", $"{host}/.env"),
            ("Properties/launchSettings.json", $"{host}/Properties/launchSettings.json"),
        };
        foreach (var (template, output) in files)
            writer.Write(output, TemplateRenderer.RenderTemplate($"V2/mcp/{template}.tpl", tokens));

        writer.Write($"{host}/Dockerfile", OpsGenerator.RenderDockerfile(config, "Mcp"));
        if (File.Exists(Path.Combine(config.SolutionPath, "docker-compose.yml")))
            writer.Write("docker-compose.mcp.yml", TemplateRenderer.RenderTemplate("V2/mcp/docker-compose.mcp.yml.tpl", tokens));

        var projects = new List<string> { $"{host}/{app}.Mcp.csproj" };
        var withTests = Directory.Exists(Path.Combine(config.SolutionPath, "tests", $"{app}.Domain.Tests"));
        if (withTests)
        {
            var tests = $"tests/{app}.Mcp.Tests";
            writer.Write($"{tests}/{app}.Mcp.Tests.csproj", TemplateRenderer.RenderTemplate("V2/mcp-tests/Mcp.Tests.csproj.tpl", tokens));
            writer.Write($"{tests}/McpFactory.cs", TemplateRenderer.RenderTemplate("V2/mcp-tests/McpFactory.cs.tpl", tokens));
            writer.Write($"{tests}/McpEndpointTests.cs", TemplateRenderer.RenderTemplate("V2/mcp-tests/McpEndpointTests.cs.tpl", tokens));
            writer.Write($"{tests}/Support/FakeUnitOfWork.cs", TemplateRenderer.RenderTemplate("V2/mcp-tests/FakeUnitOfWork.cs.tpl", tokens));
            projects.Add($"{tests}/{app}.Mcp.Tests.csproj");
        }

        foreach (var project in projects)
            ToolHost.RunCommand($"dotnet sln add {project}", config.SolutionPath);

        config.McpEnabled = true;
        ConfigManager.Save(config.SolutionPath, config);

        // Entities that already exist get their tools right away.
        foreach (var (entity, state) in config.Entities.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (state.HasCrud)
                AddEntityTools(config, entity);
            foreach (var action in FindActions(config, entity))
                AddActionTool(config, entity, action.Name, action.IsQuery);
        }

        ToolHost.Success($"MCP host added ({writer.Created.Count} files).", $"run it with: dotnet run --project {host}; clients send 'Authorization: Bearer <MCP_AUTH_TOKEN>' to /mcp");
        return true;
    }

    internal static void AddEntityTools(SolutionConfig config, string entity)
    {
        var tokens = EntityTokens(config, entity);
        var writer = new FileWriter(config.SolutionPath);
        var plural = tokens["Plural"];
        writer.Write($"src/{config.SolutionName}.Mcp/Tools/{plural}/{plural}Tools.cs", TemplateRenderer.RenderTemplate("V2/mcp-crud/Tools.cs.tpl", tokens));

        if (Directory.Exists(Path.Combine(config.SolutionPath, "tests", $"{config.SolutionName}.Mcp.Tests")))
            writer.Write($"tests/{config.SolutionName}.Mcp.Tests/Tools/{plural}ToolsTests.cs", TemplateRenderer.RenderTemplate("V2/mcp-tests/ToolsTests.cs.tpl", tokens));

        ToolHost.Info($"MCP tools generated for {entity}.", $"{tokens["ToolName"]}_list|get|create|update|delete");
    }

    internal static void AddActionTool(SolutionConfig config, string entity, string actionName, bool isQuery)
    {
        var tokens = EntityTokens(config, entity);
        tokens["ActionName"] = actionName;
        tokens["ActionSnake"] = Naming.ToKebabCase(actionName).Replace('-', '_');
        tokens["IsReadOnly"] = isQuery ? "true" : "false";
        tokens["RequestKind"] = isQuery ? "Query" : "Command";

        var plural = tokens["Plural"];
        new FileWriter(config.SolutionPath).Write(
            $"src/{config.SolutionName}.Mcp/Tools/{plural}/{plural}Tools.{actionName}.cs",
            TemplateRenderer.RenderTemplate("V2/mcp-crud/ActionTool.cs.tpl", tokens));
    }

    /// <summary>Actions of an entity, recovered from the folders <c>Actions/&lt;Action&gt;&lt;Entity&gt;</c> (a Query file means a read-only action).</summary>
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
            yield return (action, File.Exists(Path.Combine(directory, $"{name}Query.cs")));
        }
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
        };
    }

    private static Dictionary<string, string> Tokens(SolutionConfig config)
    {
        var tokens = OpsGenerator.Tokens(config);
        var provider = config.DatabaseProvider;
        var databaseName = tokens["DatabaseName"];
        var example = DatabaseProviders.ExampleConnectionString(provider, databaseName);
        var apiPort = int.TryParse(config.ApiPort, out var port) ? port : 5000;

        tokens["Provider"] = provider;
        tokens["ServerName"] = tokens["ContainerName"].Replace("-api", "-mcp");
        tokens["McpPort"] = (apiPort + 1).ToString();
        tokens["ExampleConnectionString"] = example;
        tokens["DevConnectionString"] = example;
        tokens["DevToken"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        tokens["TestConnectionExpression"] = provider == DatabaseProviders.Sqlite
            ? $"$\"Data Source={{Path.Combine(Path.GetTempPath(), $\"{config.SolutionName}-mcp-tests-{{Guid.NewGuid():N}}.db\")}}\""
            : "\"" + example + "\"";

        (tokens["ComposeConnectionString"], tokens["ComposeMcpExtras"]) = provider switch
        {
            DatabaseProviders.Postgres => (
                $"Host=db;Database={databaseName};Username=postgres;Password=${{POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}}",
                "    depends_on:\n      db:\n        condition: service_healthy"),
            DatabaseProviders.SqlServer => (
                $"Server=db,1433;Database={databaseName};User Id=sa;Password=${{SQLSERVER_PASSWORD:?set SQLSERVER_PASSWORD in .env}};TrustServerCertificate=True",
                "    depends_on:\n      db:\n        condition: service_healthy"),
            _ => ("Data Source=/data/app.db", "    volumes:\n      - data:/data")
        };

        return tokens;
    }
}
