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
        var steps = new List<IScaffoldStep>
        {
            new ProjectUpdateStep(),
        };
        // Always generate minimal entity (even in no-db)
        steps.Add(new EntityStep());
        if (!string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
            steps.Add(new DbContextStep());
        foreach (var step in steps)
            step.Execute(config, entity);

        // Determine effective CRUD style even if user supplied a CRUD action name
        var actionUpper = Upper(action).ToUpperInvariant();
        bool isCrudNamed = actionUpper is "CREATE" or "UPDATE" or "DELETE" or "GETBYID" or "GETALL" or "GETLIST";
        var effectiveCrud = crudStyle || isCrudNamed;

        if (!string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
        {
            if (effectiveCrud)
            {
                AddRepositoryMethod(config, entity, action, isCommand, true);
            }
            else
            {
                var confirm = Program.Ask($"Database detected. Add repository method '{Upper(action)}Async' for {entity}? (y/N)");
                if (!string.IsNullOrWhiteSpace(confirm) && (confirm.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) || confirm.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
                {
                    AddRepositoryMethod(config, entity, action, isCommand, false);
                }
            }
        }

        AddApplicationFiles(config, entity, action, isCommand, effectiveCrud);
        if (config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase))
            AddEndpointMethod(config, entity, action, isCommand, httpMethod, effectiveCrud);
        else
            AddControllerMethod(config, entity, action, isCommand, httpMethod, effectiveCrud);

        // ensure newly added files still have required DI registration (skip UoW for no-db)
        if (!string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
            new UnitOfWorkStep().Execute(config, entity);
        new ProjectUpdateStep().Execute(config, entity);

        if (!provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase) && !provider.Equals("None", StringComparison.OrdinalIgnoreCase))
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

    static void AddRepositoryMethod(SolutionConfig config, string entity, string action, bool isCommand, bool crudStyle)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);

        var iface = Path.Combine(config.SolutionPath, $"{solution}.Application", "Common", "Interfaces", "Repositories", $"I{entity}Repository.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(iface)!);

        var actionUpper = Upper(action);
        var hasDb = !string.Equals(config.DatabaseProvider, "None", StringComparison.OrdinalIgnoreCase);
        var cmdIsDelete = isCommand && actionUpper.Equals("Delete", StringComparison.OrdinalIgnoreCase);
        var cmdIsCreateOrUpdate = isCommand && (actionUpper.Equals("Create", StringComparison.OrdinalIgnoreCase) || actionUpper.Equals("Update", StringComparison.OrdinalIgnoreCase));

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
                ? (cmdIsDelete
                    ? $"Task {actionUpper}Async(int id);"
                    : (crudStyle && hasDb
                        ? $"Task {actionUpper}Async({entity} entity);"
                        : $"Task {actionUpper}Async();"))
                : (crudStyle
                    ? $"Task<{entity}?> {actionUpper}Async(int id);"
                    : $"Task<{entity}?> {actionUpper}Async();");
            File.WriteAllText(iface, ifaceTemplate
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural)
                .Replace("{{methodSig}}", sig));
        }
        else
        {
            var lines = File.ReadAllLines(iface).ToList();
            var sigEntity = $"{actionUpper}Async({entity} ";
            var sigId = $"{actionUpper}Async(int ";
            var sigNone = $"{actionUpper}Async()";
            bool hasEntityOverload = lines.Any(l => l.Contains(sigEntity));
            bool hasIdOverload = lines.Any(l => l.Contains(sigId));
            bool hasParamless = lines.Any(l => l.Contains(sigNone));
            bool needInsert = false;
            string sig;
            if (isCommand)
            {
                if (cmdIsDelete)
                {
                    needInsert = !hasIdOverload;
                    sig = $"    Task {actionUpper}Async(int id);";
                }
                else if (crudStyle && hasDb)
                {
                    needInsert = !hasEntityOverload;
                    sig = $"    Task {actionUpper}Async({entity} entity);";
                }
                else
                {
                    needInsert = !hasParamless;
                    sig = $"    Task {actionUpper}Async();";
                }
            }
            else
            {
                if (crudStyle)
                {
                    needInsert = !lines.Any(l => l.Contains($"Task<{entity}?> {actionUpper}Async(int id)"));
                    sig = $"    Task<{entity}?> {actionUpper}Async(int id);";
                }
                else
                {
                    needInsert = !lines.Any(l => l.Contains($"Task<{entity}?> {actionUpper}Async()"));
                    sig = $"    Task<{entity}?> {actionUpper}Async();";
                }
            }
            if (needInsert)
            {
                var idx = lines.FindLastIndex(l => l.Trim() == "}");
                lines.Insert(idx, sig);
                File.WriteAllLines(iface, lines);
            }
        }

        var impl = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", "Persistence", "Repositories", $"{entity}Repository.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(impl)!);
        var mReturn = isCommand ? "Task" : $"Task<{entity}?>";
        var param = isCommand
            ? (cmdIsDelete ? "int id" : ((crudStyle && hasDb) ? $"{entity} entity" : string.Empty))
            : (crudStyle ? "int id" : string.Empty);
        if (!File.Exists(impl))
        {
            var isCreate = actionUpper.Equals("Create", StringComparison.OrdinalIgnoreCase);
            var isUpdate = actionUpper.Equals("Update", StringComparison.OrdinalIgnoreCase);
            string body;
            if (isCommand)
            {
                if (cmdIsDelete)
                {
                    body = $"        // TODO: implement action\n        var entity = await _context.Set<{entity}>().FindAsync(id);\n        if (entity != null) _context.Set<{entity}>().Remove(entity);\n        await Task.CompletedTask;\n";
                }
                else if (crudStyle && hasDb)
                {
                    body = isCreate
                        ? $"        // TODO: implement action\n        await _context.Set<{entity}>().AddAsync(entity);\n        await Task.CompletedTask;\n"
                        : $"        // TODO: implement action\n        _context.Set<{entity}>().Update(entity);\n        await Task.CompletedTask;\n";
                }
                else
                {
                    body = "        // TODO: implement action\n        await Task.CompletedTask;\n";
                }
            }
            else
            {
                body = crudStyle
                    ? $"        // TODO: implement action\n        return await _context.Set<{entity}>().FindAsync(id);\n"
                    : $"        // TODO: implement action\n        return await Task.FromResult<{entity}?>(null);\n";
            }
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
            var hasImplEntity = lines.Any(l => l.Contains($"Task {actionUpper}Async({entity} "));
            var hasImplId = lines.Any(l => l.Contains($"Task {actionUpper}Async(int "));
            var hasImplNone = lines.Any(l => l.Contains($"Task {actionUpper}Async()"));
            bool needImpl;
            if (isCommand)
                needImpl = cmdIsDelete ? !hasImplId : (crudStyle && hasDb ? !hasImplEntity : !hasImplNone);
            else
                needImpl = !lines.Any(l => l.Contains($"Task<{entity}?> {actionUpper}Async(int id)"));
            if (needImpl)
            {
                string[] insert;
                if (isCommand)
                {
                    if (cmdIsDelete)
                    {
                        insert = new[]
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
                    else if (crudStyle && hasDb)
                    {
                        var isCreateInline = actionUpper == "Create";
                        insert = new[]
                        {
                            $"    public async Task {actionUpper}Async({entity} entity)",
                            "    {",
                            $"        // TODO: implement action",
                            isCreateInline
                                ? $"        await _context.Set<{entity}>().AddAsync(entity);"
                                : $"        _context.Set<{entity}>().Update(entity);",
                            "        await Task.CompletedTask;",
                            "    }",
                        };
                    }
                    else
                    {
                        insert = new[]
                        {
                            $"    public async Task {actionUpper}Async()",
                            "    {",
                            "        // TODO: implement action",
                            "        await Task.CompletedTask;",
                            "    }",
                        };
                    }
                }
            else
            {
                if (crudStyle)
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
                else
                {
                    insert = new[]
                    {
                        $"    public async Task<{entity}?> {actionUpper}Async()",
                        "    {",
                        "        // TODO: implement action",
                        $"        return await Task.FromResult<{entity}?>(null);",
                        "    }",
                    };
                }
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
        var noDb = string.Equals(config.DatabaseProvider, "None", StringComparison.OrdinalIgnoreCase);
        var hasDb = !noDb;
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
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill(noDb ? """
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Command>
{
    public async Task Handle({{className}}Command request, CancellationToken ct)
    {
        // No database configured. Implement deletion logic here if needed.
        await Task.CompletedTask;
    }
}
""" : """
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
            else if (hasDb && (actionName == "Create" || actionName == "Update"))
            {
                var props = ExtractScalarProps(config.SolutionPath, solution, entity, plural);
                if (actionName == "Create") props = new List<(string Type,string Name)>();
                var ctorParts = new List<string>();
                if (actionName == "Update") ctorParts.Add("int Id");
                if (props.Count > 0) ctorParts.AddRange(props.Select(p => $"{p.Type} {p.Name}"));
                var ctor = string.Join(", ", ctorParts);
                var cmdContent = string.IsNullOrEmpty(ctor)
                    ? Fill("""
using MediatR;
namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command() : IRequest;
""")
                    : Fill("""
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command(__CTOR__) : IRequest;
""").Replace("__CTOR__", ctor);
                File.WriteAllText(Path.Combine(dir, $"{className}Command.cs"), cmdContent);

                var handlerContent = (actionName == "Create" && props.Count == 0)
                    ? Fill("""
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
        var e = new {{entity}}();
        // e.Id remains default for Create
        await _uow.{{entity}}Repository.{{action}}Async(e);
        await _uow.SaveChangesAsync();
    }
}
""")
                    : Fill("""
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
        var e = new {{entity}}()
        {
__PROPS__
        };
        // Include Id for Update when present in command
__SETID__
        await _uow.{{entity}}Repository.{{action}}Async(e);
        await _uow.SaveChangesAsync();
    }
}
""")
                    .Replace("__PROPS__", string.Join("\n", props.Select(p => $"            {p.Name} = request.{p.Name},")))
                    .Replace("__SETID__", actionName == "Update" ? "        e.Id = request.Id;" : string.Empty);
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), handlerContent);
                File.WriteAllText(Path.Combine(dir, $"{className}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Validator : AbstractValidator<{{className}}Command>
{
    public {{className}}Validator()
    {
        // Add rules per properties if needed
    }
}
"""));
            }
            else if (!hasDb && crudStyle && actionName == "Update")
            {
                // No-DB standard Update: include Id in command, skeleton handler
                File.WriteAllText(Path.Combine(dir, $"{className}Command.cs"), Fill("""
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command(int Id) : IRequest;
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Command>
{
    public async Task Handle({{className}}Command request, CancellationToken ct)
    {
        // No database configured. Implement update logic here if needed.
        await Task.CompletedTask;
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
                // Non-standard single action: empty command and skeleton handler, no entity mapping
                File.WriteAllText(Path.Combine(dir, $"{className}Command.cs"), Fill(noDb ? """
using MediatR;
namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command() : IRequest;
""" : """
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public record {{className}}Command() : IRequest;
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill(noDb ? """
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Commands.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Command>
{
    public async Task Handle({{className}}Command request, CancellationToken ct)
    {
        // No database configured. Implement command logic here if needed.
        await Task.CompletedTask;
    }
}
""" : """
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
        // TODO: implement non-standard action logic here
        await Task.CompletedTask;
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
        // Add rules for command properties if needed
    }
}
"""));
            }
        }
        else
        {
            if (crudStyle)
            {
                File.WriteAllText(Path.Combine(dir, $"{className}Query.cs"), Fill(noDb ? """
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public record {{className}}Query(int Id) : IRequest<object?>;
""" : """
using MediatR;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public record {{className}}Query(int Id) : IRequest<{{entity}}?>;
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill(noDb ? """
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Query, object?>
{
    public Task<object?> Handle({{className}}Query request, CancellationToken ct)
        => Task.FromResult<object?>(null);
}
""" : """
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
            else
            {
                File.WriteAllText(Path.Combine(dir, $"{className}Query.cs"), Fill("""
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public record {{className}}Query() : IRequest<object?>;
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Handler.cs"), Fill("""
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{className}}Handler : IRequestHandler<{{className}}Query, object?>
{
    public Task<object?> Handle({{className}}Query request, CancellationToken ct)
        => Task.FromResult<object?>(null);
}
"""));
                File.WriteAllText(Path.Combine(dir, $"{className}Validator.cs"), Fill("""
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Queries.{{action}};

public class {{className}}Validator : AbstractValidator<{{className}}Query>
{
    public {{className}}Validator()
    {
        // no parameters
    }
}
"""));
            }
        }
    }

    static void AddEndpointMethod(SolutionConfig config, string entity, string action, bool isCommand, string httpMethod, bool crudStyle)
    {
        var solution = config.SolutionName;
        var startupProject = config.StartupProject;
        var plural = Naming.Pluralize(entity);
        var noDb = string.Equals(config.DatabaseProvider, "None", StringComparison.OrdinalIgnoreCase);
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
                        $"        routes.MapPost(\"/Api/{entity}\", async (IMediator mediator, Create{entity}Command command) =>",
                        "        {",
                        $"            await mediator.Send(command);",
                        "            return Results.Ok();",
                        $"        }}).WithTags(\"{entity}\");"
                    };
                    break;
                case "PUT":
                    methodLines = new List<string>
                    {
                        $"        routes.MapPut(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id, Update{entity}Command command) =>",
                        "        {",
                        $"            await mediator.Send(command with {{ Id = id }});",
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
                        $"            await mediator.Send(new {className}Query(id)) is {(noDb ? "object" : entity)} result ? Results.Ok(result) : Results.NotFound())",
                        $"            .WithTags(\"{entity}\");"
                    };
                    break;
                case "PATCH":
                    methodLines = new List<string>
                    {
                        $"        routes.MapPatch(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id, Update{entity}Command command) =>",
                        "        {",
                        $"            await mediator.Send(command with {{ Id = id }});",
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
            // Non-standard command endpoint: no payload
            methodLines = new List<string>
            {
                $"        routes.{mapCall}(\"/Api/{entity}/{actionName}\", async (IMediator mediator) =>",
                "        {",
                $"            await mediator.Send(new {className}Command());",
                "            return Results.Ok();",
                $"        }}).WithTags(\"{entity}\");"
            };
        }
        else
        {
            methodLines = new List<string>
            {
                $"        routes.{mapCall}(\"/Api/{entity}/{actionName}\", async (IMediator mediator) =>",
                $"            await mediator.Send(new {className}Query()) is object result ? Results.Ok(result) : Results.NotFound())",
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
                        $"    public async Task Create([FromBody] Create{entity}Command command)",
                        "    {",
                        $"        await _mediator.Send(command);",
                        "    }",
                        "",
                    };
                    break;
                case "PUT":
                    method = new[]
                    {
                        "    [HttpPut(\"{id}\")]",
                        $"    public async Task Update(int id, [FromBody] Update{entity}Command command)",
                        "    {",
                        $"        await _mediator.Send(command with {{ Id = id }});",
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
                        "    public async Task<IActionResult> GetById(int id)",
                        "    {",
                        $"        var result = await _mediator.Send(new {className}Query(id));",
                        "        return result is null ? NotFound() : Ok(result);",
                        "    }",
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
            // Non-standard command: take no body, send empty command
            method = new[]
            {
                $"    [{httpAttr}(\"{actionName}\")]",
                $"    public async Task<IActionResult> {actionName}()",
                "    {",
                $"        await _mediator.Send(new {className}Command());",
                "        return Ok();",
                "    }",
                "",
            };
        }
        else
        {
            method = new[]
            {
                $"    [{httpAttr}(\"{actionName}\")]",
                $"    public async Task<IActionResult> {actionName}()",
                "    {",
                $"        var result = await _mediator.Send(new {className}Query());",
                "        return result is null ? NotFound() : Ok(result);",
                "    }",
                "",
            };
        }

        if (!File.Exists(file))
        {
            var content = string.Join(Environment.NewLine, new[]
            {
                "using MediatR;",
                "using Microsoft.AspNetCore.Mvc;",
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
            var entityUsing = $"using {solution}.Core.Features.{plural}.Entities;";
            lines.RemoveAll(l => l.Trim() == entityUsing);
            var usingLine = $"using {solution}.Application.Features.{plural}.{(isCommand ? "Commands" : "Queries")}.{actionName};";
            var lastUsing = lines.FindLastIndex(l => l.StartsWith("using "));
            if (!lines.Contains(usingLine))
                lines.Insert(lastUsing + 1, usingLine);

            if (crudStyle && httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                var attrIdx = lines.FindIndex(l => l.Contains("[HttpDelete"));
                if (attrIdx != -1)
                {
                    var endIdx = attrIdx;
                    while (endIdx < lines.Count && lines[endIdx].Trim() != "")
                        endIdx++;
                    lines.RemoveRange(attrIdx, endIdx - attrIdx + 1);
                    lines.InsertRange(attrIdx, method);
                    File.WriteAllLines(file, lines);
                    return;
                }
            }

            var end = lines.FindLastIndex(l => l.Trim() == "}");
            lines.InsertRange(end, method);
            File.WriteAllLines(file, lines);
        }
    }

    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

    // Extracts scalar properties from the entity class to generate command parameters.
    static List<(string Type, string Name)> ExtractScalarProps(string solutionPath, string solutionName, string entity, string plural)
    {
        var result = new List<(string, string)>();
        try
        {
            var entityFile = Path.Combine(solutionPath, $"{solutionName}.Core", "Features", plural, "Entities", $"{entity}.cs");
            if (!File.Exists(entityFile)) return result;
            foreach (var line in File.ReadAllLines(entityFile))
            {
                var t = line.Trim();
                if (!t.StartsWith("public ") || !t.Contains("{ get; set; }")) continue;
                var parts = t.Split(new[]{' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                var type = parts[1];
                var name = parts[2];
                if (name == "Id") continue;
                if (type.StartsWith("ICollection<") || type.StartsWith("List<") || type.EndsWith("[]")) continue;
                var allowed = new HashSet<string>{"string","int","long","bool","decimal","double","float","DateTime","Guid","short","byte","char","DateOnly","TimeOnly","DateTimeOffset"};
                var baseType = type.TrimEnd('?');
                if (allowed.Contains(baseType)) result.Add((type, name));
            }
        }
        catch { }
        return result;
    }
}
