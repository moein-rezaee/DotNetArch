using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DotNetArch.Scaffolding;
using DotNetArch.Scaffolding.Steps;

static class ActionScaffolder
{
    public static void Generate(SolutionConfig config, string entity, string action, bool isCommand)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) || string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(action))
        {
            Console.WriteLine("Solution, entity and action names are required.");
            return;
        }

        var provider = config.DatabaseProvider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = DatabaseProviderSelector.Choose();
            config.DatabaseProvider = provider;
            ConfigManager.Save(config.SolutionPath, config);
        }

        if (!provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
        {
            if (!Program.EnsureEfTool(config.SolutionPath))
            {
                Console.WriteLine("❌ dotnet-ef installation failed; action generation canceled.");
                return;
            }
        }
        var steps = new IScaffoldStep[]
        {
            new ProjectUpdateStep(),
            new EntityStep(),
            new DbContextStep(),
            new RepositoryStep(),
            new UnitOfWorkStep()
        };
        foreach (var step in steps)
            step.Execute(config, entity);

        AddRepositoryMethod(config, entity, action, isCommand);
        AddApplicationFiles(config, entity, action, isCommand);
        if (config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase))
            AddEndpointMethod(config, entity, action, isCommand);
        else
            AddControllerMethod(config, entity, action, isCommand);

        if (!provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
        {
            var prev = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(config.SolutionPath);
                if (Program.RunCommand("dotnet build", config.SolutionPath))
                {
                    var infraProj = $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj";
                    var startProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
                    var migName = $"Auto_{entity}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                    if (Program.RunCommand($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir {PathConstants.MigrationsRelativePath}", config.SolutionPath))
                    {
                        Program.RunCommand($"dotnet ef database update --project {infraProj} --startup-project {startProj}", config.SolutionPath);
                    }
                }
                else
                {
                    Console.WriteLine("❌ Build failed; skipping migrations.");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(prev);
            }
        }

        if (!config.Entities.TryGetValue(entity, out var state))
            state = new EntityStatus();
        state.HasAction = true;
        config.Entities[entity] = state;
        ConfigManager.Save(config.SolutionPath, config);

        Console.WriteLine($"Action {action} for {entity} generated.");
    }

    static void AddRepositoryMethod(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);

        var iface = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces", "Repositories", $"I{entity}Repository.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(iface)!);
        if (!File.Exists(iface))
        {
            var ifaceTemplate = """
using System.Threading.Tasks;
using {{solution}}.Core.Common.Models;
using {{solution}}.Core.Features.{{entities}};

namespace {{solution}}.Application.Common.Interfaces.Repositories;

public interface I{{entity}}Repository
{
    {{methodSig}}
}
""";
            var sig = isCommand
                ? $"Task {Upper(action)}Async({entity} entity);"
                : $"Task<{entity}?> {Upper(action)}Async(int id);";
            File.WriteAllText(iface, ifaceTemplate
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural)
                .Replace("{{methodSig}}", sig));
        }
        else
        {
            var lines = File.ReadAllLines(iface).ToList();
            if (!lines.Any(l => l.Contains($"{Upper(action)}Async")))
            {
                var idx = lines.FindLastIndex(l => l.Trim() == "}");
                var sig = isCommand
                    ? $"    Task {Upper(action)}Async({entity} entity);"
                    : $"    Task<{entity}?> {Upper(action)}Async(int id);";
                lines.Insert(idx, sig);
                File.WriteAllLines(iface, lines);
            }
        }

        var impl = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Persistence", "Repositories", $"{entity}Repository.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(impl)!);
        var mReturn = isCommand ? "Task" : $"Task<{entity}?>";
        var param = isCommand ? $"{entity} entity" : "int id";
        if (!File.Exists(impl))
        {
            var body = isCommand
                ? "        // TODO: implement action\n        await Task.CompletedTask;\n"
                : $"        // TODO: implement action\n        return await _context.Set<{entity}>().FindAsync(id);\n";
            var implTemplate = """
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces.Repositories;
using {{solution}}.Core.Features.{{entities}};
using {{solution}}.Infrastructure.Persistence;

namespace {{solution}}.Infrastructure.Persistence.Repositories;

public class {{entity}}Repository : I{{entity}}Repository
{
    private readonly AppDbContext _context;
    public {{entity}}Repository(AppDbContext context) => _context = context;

    public async {{mReturn}} {{action}}Async({{param}})
    {
{{body}}    }
}
""";
            File.WriteAllText(impl, implTemplate
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural)
                .Replace("{{action}}", Upper(action))
                .Replace("{{mReturn}}", mReturn)
                .Replace("{{param}}", param)
                .Replace("{{body}}", body));
        }
        else
        {
            var lines = File.ReadAllLines(impl).ToList();
            if (!lines.Any(l => l.Contains($"{Upper(action)}Async(")))
            {
                var insert = isCommand
                    ? new[]
                    {
                        $"    public async Task {Upper(action)}Async({entity} entity)",
                        "    {",
                        "        // TODO: implement action",
                        "        await Task.CompletedTask;",
                        "    }",
                    }
                    : new[]
                    {
                        $"    public async Task<{entity}?> {Upper(action)}Async(int id)",
                        "    {",
                        "        // TODO: implement action",
                        $"        return await _context.Set<{entity}>().FindAsync(id);",
                        "    }",
                    };
                var idx = lines.FindLastIndex(l => l.Trim() == "}");
                lines.InsertRange(idx, insert);
                File.WriteAllLines(impl, lines);
            }
        }
    }

    static void AddApplicationFiles(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var appBase = Path.Combine(config.SolutionPath, $"{solution}.Application", "Features", plural);
        var dir = Path.Combine(appBase, isCommand ? "Commands" : "Queries", Upper(action));
        Directory.CreateDirectory(dir);
        string Fill(string t) => t.Replace("{{solution}}", solution)
                                  .Replace("{{entity}}", entity)
                                  .Replace("{{entities}}", plural)
                                  .Replace("{{action}}", Upper(action));
        if (isCommand)
        {
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Command.cs"), Fill("""
using MediatR;
using {{solution}}.Core.Features.{{entities}};

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{action}}{{entity}}Command({{entity}} Entity) : IRequest;
"""));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Core.Features.{{entities}};

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{action}}{{entity}}Handler : IRequestHandler<{{action}}{{entity}}Command>
{
    private readonly IUnitOfWork _uow;
    public {{action}}{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task Handle({{action}}{{entity}}Command request, CancellationToken ct)
    {
        await _uow.{{entity}}Repository.{{action}}Async(request.Entity);
        await _uow.SaveChangesAsync();
    }
}
"""));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{action}}{{entity}}Validator : AbstractValidator<{{action}}{{entity}}Command>
{
    public {{action}}{{entity}}Validator()
    {
        RuleFor(x => x.Entity).NotNull();
    }
}
"""));
        }
        else
        {
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Query.cs"), Fill("""
using MediatR;
using {{solution}}.Core.Features.{{entities}};

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public record {{action}}{{entity}}Query(int Id) : IRequest<{{entity}}?>;
"""));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Core.Features.{{entities}};

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{action}}{{entity}}Handler : IRequestHandler<{{action}}{{entity}}Query, {{entity}}?>
{
    private readonly IUnitOfWork _uow;
    public {{action}}{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}?> Handle({{action}}{{entity}}Query request, CancellationToken ct)
        => await _uow.{{entity}}Repository.{{action}}Async(request.Id);
}
"""));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{action}}{{entity}}Validator : AbstractValidator<{{action}}{{entity}}Query>
{
    public {{action}}{{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"""));
        }
    }

    static void AddEndpointMethod(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var startupProject = config.StartupProject;
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(config.SolutionPath, startupProject, "Features", plural);
        Directory.CreateDirectory(apiDir);
        var file = Path.Combine(apiDir, $"{entity}Endpoints.cs");
        var lines = File.Exists(file)
            ? File.ReadAllLines(file).ToList()
            : new List<string>
            {
                "using MediatR;",
                "using Microsoft.AspNetCore.Builder;",
                "using Microsoft.AspNetCore.Http;",
                $"using {solution}.Core.Features.{plural};",
                "",
                $"namespace {startupProject}.Features.{plural};",
                "",
                $"public static class {entity}Endpoints",
                "{",
                $"    public static void Map{entity}Endpoints(this IEndpointRouteBuilder routes)",
                "    {",
                "    }",
                "}",
            };

        var usingLine = $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{Upper(action)};";
        var lastUsing = lines.FindLastIndex(l => l.StartsWith("using "));
        if (!lines.Any(l => l.Trim() == usingLine))
            lines.Insert(lastUsing + 1, usingLine);

        var classClose = lines.FindLastIndex(l => l.Trim() == "}");
        var methodClose = lines.FindLastIndex(classClose - 1, l => l.Trim() == "}");
        var insertIndex = methodClose < 0 ? classClose : methodClose;
        if (isCommand)
        {
            var method = new[]
            {
                $"        routes.MapPost(\"/Api/{entity}/{Upper(action)}\", async (IMediator mediator, {entity} entity) =>",
                "        {",
                $"            await mediator.Send(new {Upper(action)}{entity}Command(entity));",
                "            return Results.Ok();",
                $"        }}).WithTags(\"{entity}\");",
            };
            lines.InsertRange(insertIndex, method);
        }
        else
        {
            var method = new[]
            {
                $"        routes.MapGet(\"/Api/{entity}/{Upper(action)}/{{id}}\", async (IMediator mediator, int id) =>",
                $"            await mediator.Send(new {Upper(action)}{entity}Query(id)) is {entity} result ? Results.Ok(result) : Results.NotFound())",
                $"            .WithTags(\"{entity}\");",
            };
            lines.InsertRange(insertIndex, method);
        }

        File.WriteAllLines(file, lines);
    }

    static void AddControllerMethod(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(config.SolutionPath, config.StartupProject, "Features", plural);
        Directory.CreateDirectory(apiDir);
        var file = Path.Combine(apiDir, $"{entity}Controller.cs");
        var method = isCommand
            ? new[]
            {
                $"    [HttpPost(\"{Upper(action)}\")]",
                $"    public async Task<IActionResult> {Upper(action)}([FromBody] {entity} entity)",
                "    {",
                $"        await _mediator.Send(new {Upper(action)}{entity}Command(entity));",
                "        return Ok();",
                "    }",
                ""
            }
            : new[]
            {
                $"    [HttpGet(\"{Upper(action)}/{{id}}\")]",
                $"    public async Task<{entity}?> {Upper(action)}(int id)",
                "        => await _mediator.Send(new " + Upper(action) + entity + "Query(id));",
                ""
            };
        if (!File.Exists(file))
        {
            var content = string.Join(Environment.NewLine, new[]
            {
                "using MediatR;",
                "using Microsoft.AspNetCore.Mvc;",
                $"using {solution}.Core.Features.{plural};",
                $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{Upper(action)};",
                "",
                $"namespace {config.StartupProject}.Features.{plural};",
                "",
                "[ApiController]",
                "[Route(\"Api/[controller]\")]",
                $"public class {entity}Controller : ControllerBase",
                "{",
                "    private readonly IMediator _mediator;",
                $"    public {entity}Controller(IMediator mediator) => _mediator = mediator;",
                ""
            }.Concat(method).Concat(new[]{"}"}));
            File.WriteAllText(file, content);
        }
        else
        {
            var lines = File.ReadAllLines(file).ToList();
            var usingLine = $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{Upper(action)};";
            var lastUsing = lines.FindLastIndex(l => l.StartsWith("using "));
            if (!lines.Contains(usingLine))
                lines.Insert(lastUsing + 1, usingLine);
            var end = lines.FindLastIndex(l => l.Trim() == "}");
            lines.InsertRange(end, method);
            File.WriteAllLines(file, lines);
        }
    }

    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
}
