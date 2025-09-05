using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DotNetArch.Scaffolding;
using DotNetArch.Scaffolding.Steps;

static class ActionScaffolder
{
    public static void Generate(SolutionConfig config, string entity, string action, string httpMethod, bool crudStyle)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) || string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(action))
        {
            Program.Error("Solution, entity and action names are required.");
            return;
        }

        var isCommand = !httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase);

        if (ActionExists(config, entity, action, isCommand, httpMethod, crudStyle))
        {
            Program.Error("Action with the same method already exists for this entity.");
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
                Program.Error("dotnet-ef installation failed; action generation canceled.");
                return;
            }
        }
        var steps = new IScaffoldStep[]
        {
            new ProjectUpdateStep(),
            new EntityStep(),
            new DbContextStep()
        };
        foreach (var step in steps)
            step.Execute(config, entity);

        AddRepositoryMethod(config, entity, action, isCommand);
        AddApplicationFiles(config, entity, action, isCommand, crudStyle);
        if (config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase))
            AddEndpointMethod(config, entity, action, isCommand, httpMethod, crudStyle);
        else
            AddControllerMethod(config, entity, action, isCommand, httpMethod, crudStyle);

        // ensure newly added files still have required DI registration
        new UnitOfWorkStep().Execute(config, entity);
        new ProjectUpdateStep().Execute(config, entity);

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
                    Program.Error("Build failed; skipping migrations.");
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

        Program.Success($"Action {action} for {entity} generated.");
    }

    static bool ActionExists(SolutionConfig config, string entity, string action, bool isCommand, string httpMethod, bool crudStyle)
    {
        var plural = Naming.Pluralize(entity);
        if (config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase))
        {
            var file = Path.Combine(config.SolutionPath, config.StartupProject, "Features", plural, $"{entity}Endpoints.cs");
            if (!File.Exists(file))
                return false;
            var lines = File.ReadAllLines(file);
            if (crudStyle)
            {
                var pattern = httpMethod.ToUpper() switch
                {
                    "GET" => $"MapGet(\"/Api/{entity}/{{id}}\"",
                    "POST" => $"MapPost(\"/Api/{entity}\"",
                    "PUT" => $"MapPut(\"/Api/{entity}/{{id}}\"",
                    "DELETE" => $"MapDelete(\"/Api/{entity}/{{id}}\"",
                    "PATCH" => $"MapPatch(\"/Api/{entity}/{{id}}\"",
                    _ => string.Empty
                };
                return lines.Any(l => l.Contains(pattern));
            }
            else
            {
                var mapCall = httpMethod.ToUpper() switch
                {
                    "GET" => "MapGet",
                    "POST" => "MapPost",
                    "PUT" => "MapPut",
                    "DELETE" => "MapDelete",
                    "PATCH" => "MapPatch",
                    _ => "MapGet"
                };
                var prefix = isCommand
                    ? $"{mapCall}(\"/Api/{entity}/{Upper(action)}"
                    : $"{mapCall}(\"/Api/{entity}/{Upper(action)}/";
                return lines.Any(l => l.Contains(prefix));
            }
        }
        else
        {
            var file = Path.Combine(config.SolutionPath, config.StartupProject, "Features", plural, $"{entity}Controller.cs");
            if (!File.Exists(file))
                return false;
            var lines = File.ReadAllLines(file);
            var httpAttr = httpMethod.ToUpper() switch
            {
                "GET" => "HttpGet",
                "POST" => "HttpPost",
                "PUT" => "HttpPut",
                "DELETE" => "HttpDelete",
                "PATCH" => "HttpPatch",
                _ => "HttpGet"
            };
            if (crudStyle)
            {
                var pattern = httpMethod.ToUpper() switch
                {
                    "POST" => "[HttpPost]",
                    "GET" => "[HttpGet(\"{id}\")]",
                    "PUT" => "[HttpPut(\"{id}\")]",
                    "DELETE" => "[HttpDelete(\"{id}\")]",
                    "PATCH" => "[HttpPatch(\"{id}\")]",
                    _ => string.Empty
                };
                return lines.Any(l => l.Contains(pattern));
            }
            else
            {
                var pattern = $"[{httpAttr}(\"{Upper(action)}";
                return lines.Any(l => l.Contains(pattern));
            }
        }
    }

    static void AddRepositoryMethod(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);

        var iface = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces", "Repositories", $"I{entity}Repository.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(iface)!);

        var actionUpper = Upper(action);
        var cmdUsesEntity = isCommand && !actionUpper.Equals("Delete", StringComparison.OrdinalIgnoreCase);

        if (!File.Exists(iface))
        {
            var ifaceTemplate = """
using System.Threading.Tasks;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Common.Interfaces.Repositories;

public interface I{{entity}}Repository
{
    {{methodSig}}
}
""";
            var sig = isCommand
                ? cmdUsesEntity
                    ? $"Task {actionUpper}Async({entity} entity);"
                    : $"Task {actionUpper}Async(int id);"
                : $"Task<{entity}?> {actionUpper}Async(int id);";
            File.WriteAllText(iface, ifaceTemplate
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural)
                .Replace("{{methodSig}}", sig));
        }
        else
        {
            var lines = File.ReadAllLines(iface).ToList();
            if (!lines.Any(l => l.Contains($"{actionUpper}Async")))
            {
                var idx = lines.FindLastIndex(l => l.Trim() == "}");
                var sig = isCommand
                    ? cmdUsesEntity
                        ? $"    Task {actionUpper}Async({entity} entity);"
                        : $"    Task {actionUpper}Async(int id);"
                    : $"    Task<{entity}?> {actionUpper}Async(int id);";
                lines.Insert(idx, sig);
                File.WriteAllLines(iface, lines);
            }
        }

        var impl = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Persistence", "Repositories", $"{entity}Repository.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(impl)!);
        var mReturn = isCommand ? "Task" : $"Task<{entity}?>";
        var param = isCommand
            ? cmdUsesEntity ? $"{entity} entity" : "int id"
            : "int id";
        if (!File.Exists(impl))
        {
            var body = isCommand
                ? cmdUsesEntity
                    ? "        // TODO: implement action\n        await Task.CompletedTask;\n"
                    : $"        // TODO: implement action\n        var entity = await _context.Set<{entity}>().FindAsync(id);\n        if (entity != null) _context.Set<{entity}>().Remove(entity);\n        await Task.CompletedTask;\n"
                : $"        // TODO: implement action\n        return await _context.Set<{entity}>().FindAsync(id);\n";
            var implTemplate = """
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces.Repositories;
using {{solution}}.Core.Features.{{entities}}.Entities;
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
            if (!lines.Any(l => l.Contains($"{actionUpper}Async(")))
            {
                string[] insert;
                if (isCommand)
                {
                    insert = cmdUsesEntity
                        ? new[]
                        {
                            $"    public async Task {actionUpper}Async({entity} entity)",
                            "    {",
                            "        // TODO: implement action",
                            "        await Task.CompletedTask;",
                            "    }",
                        }
                        : new[]
                        {
                            $"    public async Task {actionUpper}Async(int id)",
                            "    {",
                            $"        // TODO: implement action",
                            $"        var entity = await _context.Set<{entity}>().FindAsync(id);",
                            $"        if (entity != null) _context.Set<{entity}>().Remove(entity);",
                            "        await Task.CompletedTask;",
                            "    }",
                        };
                }
                else
                {
                    insert = new[]
                    {
                        $"    public async Task<{entity}?> {actionUpper}Async(int id)",
                        "    {",
                        "        // TODO: implement action",
                        $"        return await _context.Set<{entity}>().FindAsync(id);",
                        "    }",
                    };
                }
                var idx = lines.FindLastIndex(l => l.Trim() == "}");
                lines.InsertRange(idx, insert);
                File.WriteAllLines(impl, lines);
            }
        }
    }

    static void AddApplicationFiles(SolutionConfig config, string entity, string action, bool isCommand, bool crudStyle)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var appBase = Path.Combine(config.SolutionPath, $"{solution}.Application", "Features", plural);
        var dir = Path.Combine(appBase, isCommand ? "Commands" : "Queries", Upper(action));
        Directory.CreateDirectory(dir);
        var actionName = Upper(action);
        var className = crudStyle && actionName == "GetById" ? $"Get{entity}ById" : actionName + entity;
        string Fill(string t) => t.Replace("{{solution}}", solution)
                                  .Replace("{{entity}}", entity)
                                  .Replace("{{entities}}", plural)
                                  .Replace("{{action}}", actionName)
                                  .Replace("{{className}}", className);
        if (isCommand)
        {
            if (crudStyle && actionName == "Delete")
            {
                File.WriteAllText(Path.Combine(dir, $"{className}Command.cs"), Fill("""
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command(int Id) : IRequest;
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Command>
{
    private readonly IUnitOfWork _uow;
    public {{className}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task Handle({{className}}Command request, CancellationToken ct)
    {
        await _uow.{{entity}}Repository.{{action}}Async(request.Id);
        await _uow.SaveChangesAsync();
    }
}
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Validator : AbstractValidator<{{className}}Command>
{
    public {{className}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"""));
            }
            else
            {
                File.WriteAllText(Path.Combine(dir, $"{className}Command.cs"), Fill("""
using MediatR;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command({{entity}} Entity) : IRequest;
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Command>
{
    private readonly IUnitOfWork _uow;
    public {{className}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task Handle({{className}}Command request, CancellationToken ct)
    {
        await _uow.{{entity}}Repository.{{action}}Async(request.Entity);
        await _uow.SaveChangesAsync();
    }
}
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Validator : AbstractValidator<{{className}}Command>
{
    public {{className}}Validator()
    {
        RuleFor(x => x.Entity).NotNull();
    }
}
"""));
            }
        }
        else
        {
            File.WriteAllText(Path.Combine(dir, $"{className}Query.cs"), Fill("""
using MediatR;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public record {{className}}Query(int Id) : IRequest<{{entity}}?>;
"""));
            File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Query, {{entity}}?>
{
    private readonly IUnitOfWork _uow;
    public {{className}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}?> Handle({{className}}Query request, CancellationToken ct)
        => await _uow.{{entity}}Repository.{{action}}Async(request.Id);
}
"""));
            File.WriteAllText(Path.Combine(dir, $"{className}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{className}}Validator : AbstractValidator<{{className}}Query>
{
    public {{className}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"""));
        }
    }

    static void AddEndpointMethod(SolutionConfig config, string entity, string action, bool isCommand, string httpMethod, bool crudStyle)
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
                $"using {solution}.Core.Features.{plural}.Entities;",
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

        var actionName = Upper(action);
        var className = crudStyle && actionName == "GetById" ? $"Get{entity}ById" : actionName + entity;
        var usingLine = $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{actionName};";
        var lastUsing = lines.FindLastIndex(l => l.StartsWith("using "));
        if (!lines.Any(l => l.Trim() == usingLine))
            lines.Insert(lastUsing + 1, usingLine);

        var classClose = lines.FindLastIndex(l => l.Trim() == "}");
        var methodClose = lines.FindLastIndex(classClose - 1, l => l.Trim() == "}");
        var insertIndex = methodClose < 0 ? classClose : methodClose;
        var mapCall = httpMethod.ToUpper() switch
        {
            "GET" => "MapGet",
            "POST" => "MapPost",
            "PUT" => "MapPut",
            "DELETE" => "MapDelete",
            "PATCH" => "MapPatch",
            _ => "MapGet"
        };

        List<string> methodLines;
        if (crudStyle)
        {
            switch (httpMethod.ToUpper())
            {
                case "POST":
                    methodLines = new List<string>
                    {
                        $"        routes.MapPost(\"/Api/{entity}\", async (IMediator mediator, {entity} entity) =>",
                        "        {",
                        $"            await mediator.Send(new {className}Command(entity));",
                        "            return Results.Ok();",
                        $"        }}).WithTags(\"{entity}\");"
                    };
                    break;
                case "PUT":
                    methodLines = new List<string>
                    {
                        $"        routes.MapPut(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id, {entity} entity) =>",
                        "        {",
                        "            entity.Id = id;",
                        $"            await mediator.Send(new {className}Command(entity));",
                        "            return Results.NoContent();",
                        $"        }}).WithTags(\"{entity}\");"
                    };
                    break;
                case "DELETE":
                    methodLines = new List<string>
                    {
                        $"        routes.MapDelete(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id) =>",
                        "        {",
                        $"            await mediator.Send(new {className}Command(id));",
                        "            return Results.NoContent();",
                        $"        }}).WithTags(\"{entity}\");"
                    };
                    break;
                case "GET":
                    methodLines = new List<string>
                    {
                        $"        routes.MapGet(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id) =>",
                        $"            await mediator.Send(new {className}Query(id)) is {entity} result ? Results.Ok(result) : Results.NotFound())",
                        $"            .WithTags(\"{entity}\");"
                    };
                    break;
                case "PATCH":
                    methodLines = new List<string>
                    {
                        $"        routes.MapPatch(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id, {entity} entity) =>",
                        "        {",
                        "            entity.Id = id;",
                        $"            await mediator.Send(new {className}Command(entity));",
                        "            return Results.NoContent();",
                        $"        }}).WithTags(\"{entity}\");"
                    };
                    break;
                default:
                    methodLines = new List<string>();
                    break;
            }
        }
        else if (isCommand)
        {
            methodLines = new List<string>
            {
                $"        routes.{mapCall}(\"/Api/{entity}/{actionName}\", async (IMediator mediator, {entity} entity) =>",
                "        {",
                $"            await mediator.Send(new {className}Command(entity));",
                "            return Results.Ok();",
                $"        }}).WithTags(\"{entity}\");"
            };
        }
        else
        {
            methodLines = new List<string>
            {
                $"        routes.{mapCall}(\"/Api/{entity}/{actionName}/{{id}}\", async (IMediator mediator, int id) =>",
                $"            await mediator.Send(new {className}Query(id)) is {entity} result ? Results.Ok(result) : Results.NotFound())",
                $"            .WithTags(\"{entity}\");"
            };
        }

        lines.InsertRange(insertIndex, methodLines);

        File.WriteAllLines(file, lines);
    }

    static void AddControllerMethod(SolutionConfig config, string entity, string action, bool isCommand, string httpMethod, bool crudStyle)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(config.SolutionPath, config.StartupProject, "Features", plural);
        Directory.CreateDirectory(apiDir);
        var file = Path.Combine(apiDir, $"{entity}Controller.cs");
        var httpAttr = httpMethod.ToUpper() switch
        {
            "GET" => "HttpGet",
            "POST" => "HttpPost",
            "PUT" => "HttpPut",
            "DELETE" => "HttpDelete",
            "PATCH" => "HttpPatch",
            _ => "HttpGet"
        };
        var actionName = Upper(action);
        var className = crudStyle && actionName == "GetById" ? $"Get{entity}ById" : actionName + entity;
        string[] method;
        if (crudStyle)
        {
            switch (httpMethod.ToUpper())
            {
                case "POST":
                    method = new[]
                    {
                        "    [HttpPost]",
                        $"    public async Task Create([FromBody] {entity} entity)",
                        "    {",
                        $"        await _mediator.Send(new {className}Command(entity));",
                        "    }",
                        "",
                    };
                    break;
                case "PUT":
                    method = new[]
                    {
                        "    [HttpPut(\"{id}\")]",
                        $"    public async Task Update(int id, [FromBody] {entity} entity)",
                        "    {",
                        "        entity.Id = id;",
                        $"        await _mediator.Send(new {className}Command(entity));",
                        "    }",
                        "",
                    };
                    break;
                case "DELETE":
                    method = new[]
                    {
                        "    [HttpDelete(\"{id}\")]",
                        $"    public async Task Delete(int id) => await _mediator.Send(new {className}Command(id));",
                        "",
                    };
                    break;
                case "GET":
                    method = new[]
                    {
                        "    [HttpGet(\"{id}\")]",
                        $"    public async Task<{entity}?> GetById(int id)",
                        $"        => await _mediator.Send(new {className}Query(id));",
                        "",
                    };
                    break;
                case "PATCH":
                    method = new[]
                    {
                        "    [HttpPatch(\"{id}\")]",
                        $"    public async Task Patch(int id, [FromBody] {entity} entity)",
                        "    {",
                        "        entity.Id = id;",
                        $"        await _mediator.Send(new {className}Command(entity));",
                        "    }",
                        "",
                    };
                    break;
                default:
                    method = Array.Empty<string>();
                    break;
            }
        }
        else if (isCommand)
        {
            method = new[]
            {
                $"    [{httpAttr}(\"{actionName}\")]",
                $"    public async Task<IActionResult> {actionName}([FromBody] {entity} entity)",
                "    {",
                $"        await _mediator.Send(new {className}Command(entity));",
                "        return Ok();",
                "    }",
                "",
            };
        }
        else
        {
            method = new[]
            {
                $"    [{httpAttr}(\"{actionName}/{{id}}\")]",
                $"    public async Task<{entity}?> {actionName}(int id)",
                $"        => await _mediator.Send(new {className}Query(id));",
                "",
            };
        }

        if (!File.Exists(file))
        {
            var content = string.Join(Environment.NewLine, new[]
            {
                "using MediatR;",
                "using Microsoft.AspNetCore.Mvc;",
                $"using {solution}.Core.Features.{plural}.Entities;",
                $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{actionName};",
                "",
                $"namespace {config.StartupProject}.Features.{plural};",
                "",
                "[ApiController]",
                "[Route(\"Api/[controller]\")]",
                $"public class {entity}Controller : ControllerBase",
                "{",
                "    private readonly IMediator _mediator;",
                $"    public {entity}Controller(IMediator mediator) => _mediator = mediator;",
                "",
            }.Concat(method).Concat(new[]{"}"}));
            File.WriteAllText(file, content);
        }
        else
        {
            var lines = File.ReadAllLines(file).ToList();
            var usingLine = $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{actionName};";
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
