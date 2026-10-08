using System.ComponentModel;
using DotNetArch.Core.Config;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Kits;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Validation;
using ModelContextProtocol.Server;

namespace DotNetArch.Mcp.Tools;

/// <summary>
/// The DotNetArch generators as MCP tools. Every tool runs non-interactively: anything the CLI would ask is a parameter here,
/// and a missing required value is reported instead of prompted. Each result lists the created and modified files and the
/// equivalent CLI command.
/// </summary>
[McpServerToolType]
public sealed class DotNetArchTools(IProcessRunner runner)
{
    [McpServerTool(Name = "new_solution", Destructive = false)]
    [Description("Create a new solution (layout v2: Clean Architecture microservice with CQRS, repository/unit of work, tests, optional Docker/CI).")]
    public Task<ToolResult> NewSolution(
        [Description("Solution name, letters/digits/underscores, dots allowed between segments.")] string name,
        [Description("Folder in which the solution folder is created.")] string outputPath,
        [Description("SQLite, SqlServer or Postgres (layout v2).")] string database = "SQLite",
        [Description("controller or fast (minimal API).")] string apiStyle = "controller",
        [Description("v2 (default) or legacy.")] string layout = "v2",
        [Description("CI provider: auto, none, github, gitlab, azure, bitbucket or gitea.")] string ci = "none",
        [Description("Git remote URL to configure as origin.")] string? gitRemote = null,
        [Description("Git provider for personal servers: github, gitlab, gitea, azure, bitbucket.")] string? gitProvider = null,
        [Description("Base URL of a personal/self-hosted git server.")] string? gitHost = null,
        [Description("Private Docker registry (host[/path]).")] string? dockerRegistry = null,
        [Description("Private NuGet feed URL; credentials come from CI secrets, never from files.")] string? nugetSource = null,
        [Description("Name of the private NuGet feed.")] string? nugetSourceName = null,
        [Description("Skip Dockerfile/compose.")] bool noDocker = false,
        [Description("Skip git init.")] bool noGit = false,
        [Description("Skip the generated test projects.")] bool noTests = false,
        [Description("Also generate an MCP host (src/<App>.Mcp) whose tools mirror every entity.")] bool mcp = false) =>
        Run($"dotnet-arch new solution {name} --output={outputPath} --database={database} --style={apiStyle} --layout={layout} --ci={ci}",
            Path.Combine(outputPath, name),
            () =>
            {
                var ops = new OpsOptions(ci, gitRemote, gitHost, gitProvider, dockerRegistry, nugetSource, nugetSourceName, noDocker, noGit, noTests, mcp);
                var startup = $"{name}.{(layout.Equals("legacy", StringComparison.OrdinalIgnoreCase) ? "API" : "Api")}";
                var provider = layout.Equals("legacy", StringComparison.OrdinalIgnoreCase) && database.Equals("None", StringComparison.OrdinalIgnoreCase) ? "None" : database;
                SolutionGenerator.Generate(new SolutionRequest(name, outputPath, startup, apiStyle, provider, layout, ops));
            });

    [McpServerTool(Name = "new_crud", Destructive = false)]
    [Description("Generate the CRUD vertical slice (commands, queries, DTOs, validators, EF mapping, controller/endpoints, tests) for an entity.")]
    public Task<ToolResult> NewCrud(
        [Description("Folder containing dotnet-arch.yml.")] string solutionPath,
        [Description("Entity name, singular PascalCase.")] string entity,
        [Description("Do not create an EF migration.")] bool skipMigration = false) =>
        InSolution($"dotnet-arch new crud --entity={entity} --output={solutionPath}", solutionPath, skipMigration, config => CrudScaffolder.Generate(config, Identifier.RequireIdentifier(entity, "entity name")));

    [McpServerTool(Name = "new_action", Destructive = false)]
    [Description("Add a custom use case to an existing entity: domain method stub, command/query, handler, validator and endpoint.")]
    public Task<ToolResult> NewAction(
        string solutionPath,
        [Description("Existing entity.")] string entity,
        [Description("Action name, for example Archive.")] string action,
        [Description("GET, POST, PUT, PATCH or DELETE.")] string httpMethod,
        bool skipMigration = true) =>
        InSolution($"dotnet-arch new action --entity={entity} --action={action} --method={httpMethod} --output={solutionPath}", solutionPath, skipMigration,
            config => ActionScaffolder.Generate(config, entity, action, httpMethod.ToUpperInvariant(), crudStyle: false));

    [McpServerTool(Name = "new_event", Destructive = false)]
    [Description("Create a domain event for an entity and optionally subscribers (handlers in other entities' feature folders).")]
    public Task<ToolResult> NewEvent(
        string solutionPath,
        string entity,
        [Description("Event name, for example Created.")] string eventName,
        [Description("Entities that react to the event.")] string[]? subscribers = null) =>
        InSolution($"dotnet-arch new event --entity={entity} --name={eventName} --output={solutionPath}", solutionPath, true, config =>
        {
            if (!EventScaffolder.GenerateEvent(config, entity, eventName))
                return;

            foreach (var subscriber in subscribers ?? Array.Empty<string>())
                EventScaffolder.AddSubscriber(config, entity, eventName, subscriber);
        });

    [McpServerTool(Name = "new_enum", Destructive = false)]
    [Description("Create an enum, scoped to an entity or common to the domain when the entity is omitted.")]
    public Task<ToolResult> NewEnum(string solutionPath, string enumName, string? entity = null) =>
        InSolution($"dotnet-arch new enum --enum={enumName} --output={solutionPath}", solutionPath, true, config => EnumScaffolder.Generate(config, entity, enumName));

    [McpServerTool(Name = "new_constant", Destructive = false)]
    [Description("Create a constants class, scoped to an entity or common to the domain when the entity is omitted.")]
    public Task<ToolResult> NewConstant(string solutionPath, string constantName, string? entity = null) =>
        InSolution($"dotnet-arch new constant --constant={constantName} --output={solutionPath}", solutionPath, true, config => ConstantScaffolder.Generate(config, entity, constantName));

    [McpServerTool(Name = "new_service", Destructive = false)]
    [Description("Add a service. With business logic: an Application service. Without (an external capability): a kit with providers, wired into the composition root.")]
    public Task<ToolResult> NewService(
        string solutionPath,
        [Description("True: internal service with logic. False: external capability, generates a kit.")] bool hasBusinessLogic,
        [Description("Service name (when hasBusinessLogic).")] string? name = null,
        [Description("Optional entity whose feature folder hosts the service.")] string? entity = null,
        [Description("Scoped, Transient or Singleton.")] string lifetime = "Scoped",
        [Description("Kit area (when not hasBusinessLogic): MediaStorage, Cache, MessageBroker or a new capability name.")] string? area = null,
        [Description("Kit providers, for example Redis and InMemory.")] string[]? providers = null,
        bool withTests = false) =>
        InSolution(hasBusinessLogic
                ? $"dotnet-arch new service --logic=true --name={name} --output={solutionPath}"
                : $"dotnet-arch new service --logic=false --area={area} --output={solutionPath}",
            solutionPath, true,
            config =>
            {
                if (!config.IsV2)
                    throw new InvalidOperationException("new_service needs a layout v2 solution.");

                if (hasBusinessLogic)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        throw new MissingInputException("name");
                    ServiceV2Generator.GenerateInternal(config, entity, name, lifetime);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(area))
                        throw new MissingInputException("area");
                    ServiceV2Generator.GenerateKit(config, area, providers ?? Array.Empty<string>(), withTests);
                }
            });

    [McpServerTool(Name = "new_kit", Destructive = false)]
    [Description("Generate an independent kit (Abstractions + Core + providers) under kits/<Area>, optionally wiring it into the solution.")]
    public Task<ToolResult> NewKit(
        [Description("Solution folder (with dotnet-arch.yml) or any folder to create kits/<Area> in.")] string path,
        string area,
        string[]? providers = null,
        [Description("Package prefix; defaults to the solution name.")] string? kitPrefix = null,
        bool withTests = false,
        [Description("Wire the kit into the solution (needs a layout v2 solution).")] bool wire = true) =>
        Run($"dotnet-arch new kit --area={area} --output={path}", path, () =>
        {
            var config = ConfigManager.Load(path);
            var prefix = kitPrefix ?? config?.KitPrefix;
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = config?.SolutionName;
            if (string.IsNullOrWhiteSpace(prefix))
                throw new MissingInputException("kitPrefix");

            var major = config is null ? 8 : PackageVersionResolver.ResolveTargetMajor(config.TargetFramework);
            var info = KitGenerator.Generate(new KitRequest(config?.SolutionPath ?? path, area, providers ?? Array.Empty<string>(), prefix, major, withTests));
            if (config is not null && wire)
                KitWiring.Wire(config, info);
        });

    [McpServerTool(Name = "add_kit", Destructive = false)]
    [Description("Wire an existing kit (kits/<Area>) into the solution.")]
    public Task<ToolResult> AddKit(string solutionPath, string area) =>
        InSolution($"dotnet-arch add kit {area} --output={solutionPath}", solutionPath, true, config => KitWiring.WireFromDisk(config, KitGenerator.NormalizeArea(area)));

    [McpServerTool(Name = "add_mcp", Destructive = false)]
    [Description("Add an MCP host (src/<App>.Mcp) to a layout v2 solution; existing and future entities and actions get tools that run the same use cases as the API.")]
    public Task<ToolResult> AddMcp(string solutionPath) =>
        InSolution($"dotnet-arch add mcp --output={solutionPath}", solutionPath, true, config => McpV2Generator.Add(config));

    [McpServerTool(Name = "ci_add", Destructive = false)]
    [Description("Add a CI pipeline for the git provider (auto-detected from the remote when 'auto').")]
    public Task<ToolResult> CiAdd(string solutionPath, [Description("auto, github, gitlab, azure, bitbucket or gitea.")] string provider = "auto") =>
        InSolution($"dotnet-arch ci add {provider} --output={solutionPath}", solutionPath, true, config =>
        {
            var resolved = OpsGenerator.ResolveCiProvider(config, provider)
                ?? throw new MissingInputException("provider (could not be detected from the git remote)");
            OpsGenerator.AddCi(config, resolved);
        });

    [McpServerTool(Name = "docker_add", Destructive = false)]
    [Description("Add Dockerfile, compose file and .dockerignore; optionally set a private registry / NuGet feed.")]
    public Task<ToolResult> DockerAdd(string solutionPath, string? dockerRegistry = null, string? nugetSource = null, string? nugetSourceName = null) =>
        InSolution($"dotnet-arch docker add --output={solutionPath}", solutionPath, true, config =>
        {
            if (!string.IsNullOrWhiteSpace(dockerRegistry))
                config.DockerRegistry = dockerRegistry;
            if (!string.IsNullOrWhiteSpace(nugetSource))
                config.NuGetSource = nugetSource;
            if (!string.IsNullOrWhiteSpace(nugetSourceName))
                config.NuGetSourceName = nugetSourceName;
            OpsGenerator.AddNuGetConfig(config);
            OpsGenerator.AddDocker(config);
        });

    [McpServerTool(Name = "git_setup", Destructive = false)]
    [Description("Configure the git remote and record the git provider/host (personal servers supported).")]
    public Task<ToolResult> GitSetup(string solutionPath, string? remote = null, string? gitProvider = null, string? gitHost = null) =>
        InSolution($"dotnet-arch git setup --output={solutionPath}", solutionPath, true, config => OpsGenerator.SetupGit(config, remote, gitHost, gitProvider));

    [McpServerTool(Name = "list_entities", ReadOnly = true)]
    [Description("List the entities, kits and settings recorded in the solution's dotnet-arch.yml.")]
    public Task<ToolResult> ListEntities(string solutionPath) =>
        Task.FromResult(Describe(solutionPath, entitiesOnly: true));

    [McpServerTool(Name = "doctor", ReadOnly = true)]
    [Description("Diagnose an existing repository against the DotNetArch standard (layers, dependency direction, tests, build hygiene, configuration, secrets, Docker, CI, docs, code rules, optional Corevia governance). Read-only; returns findings with locations and fixes.")]
    public Task<ToolResult> Doctor(
        [Description("Repository root to diagnose.")] string repositoryPath,
        [Description("auto (default), generic or corevia.")] string profile = "auto",
        [Description("Return the JSON report instead of the text report.")] bool json = false)
    {
        var command = $"dotnet-arch doctor {repositoryPath} --profile={profile}{(json ? " --json" : string.Empty)}";
        try
        {
            if (!Enum.TryParse<DoctorProfile>(profile, ignoreCase: true, out var parsed))
                return Task.FromResult(new ToolResult(false, command, string.Empty, [], [], $"Unknown profile '{profile}'. Use auto, generic or corevia."));
            var report = DoctorRunner.Run(repositoryPath, new DoctorOptions(parsed));
            return Task.FromResult(new ToolResult(report.IsHealthy(), command, json ? DoctorFormatter.ToJson(report) : DoctorFormatter.ToText(report), [], [], report.IsHealthy() ? null : $"{report.Errors} blocking error(s)"));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(new ToolResult(false, command, string.Empty, [], [], ex.Message));
        }
    }

    [McpServerTool(Name = "describe_config", ReadOnly = true)]
    [Description("Describe the solution configuration: layout, framework, database, API style, CI/git/registry settings and wired kits.")]
    public Task<ToolResult> DescribeConfig(string solutionPath) =>
        Task.FromResult(Describe(solutionPath, entitiesOnly: false));

    // ---------------------------------------------------------------------------------------------------------------
    private ToolResult Describe(string solutionPath, bool entitiesOnly)
    {
        var config = ConfigManager.Load(solutionPath);
        if (config is null)
            return new ToolResult(false, "dotnet-arch describe", string.Empty, Array.Empty<string>(), Array.Empty<string>(), $"No dotnet-arch.yml found in '{solutionPath}'.");

        var lines = new List<string>();
        foreach (var (name, state) in config.Entities.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            lines.Add($"entity {name}: crud={state.HasCrud} actions={state.HasAction}");
        foreach (var (area, providers) in config.Kits.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            lines.Add($"kit {area}: {providers}");

        if (!entitiesOnly)
        {
            lines.Insert(0, $"solution={config.SolutionName} layout={config.Layout} framework={config.TargetFramework} database={config.DatabaseProvider} style={config.ApiStyle} port={config.ApiPort}");
            lines.Insert(1, $"ci={config.CiProvider} gitProvider={config.GitProvider} gitHost={config.GitHost} dockerRegistry={config.DockerRegistry} nugetSource={config.NuGetSource}");
        }

        return new ToolResult(true, "dotnet-arch describe", string.Join('\n', lines), Array.Empty<string>(), Array.Empty<string>());
    }

    private Task<ToolResult> InSolution(string cli, string solutionPath, bool skipMigration, Action<SolutionConfig> action) =>
        Run(cli, solutionPath, () =>
        {
            var config = ConfigManager.Load(solutionPath)
                ?? throw new InvalidOperationException($"Solution configuration (dotnet-arch.yml) not found in '{solutionPath}'. Run new_solution first.");
            action(config);
        }, skipMigration);

    private Task<ToolResult> Run(string cli, string trackedRoot, Action action, bool skipMigration = false) =>
        Task.Run(() =>
        {
            var output = new BufferedToolOutput();
            using var scope = ToolHost.Use(new HostContext(new NonInteractivePrompter(), output, runner, skipMigration));
            var changes = FileChanges.Begin(trackedRoot);
            string? error = null;
            try
            {
                action();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or MissingInputException or IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }

            var (created, modified) = changes.End();
            var ok = error is null && output.ErrorCount == 0;
            return new ToolResult(ok, cli, output.ToString(), created, modified, error);
        });
}
