using System;
using System.IO;
using System.Linq;
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
        var steps = new IScaffoldStep[]
        {
            new ProjectUpdateStep(),
            new EntityStep(),
            new DbContextStep(),
            new RepositoryStep(),
            new UnitOfWorkStep()
        };
        foreach (var step in steps)
            step.Execute(config.SolutionName, entity, provider, config.SolutionPath, config.StartupProject);

        AddRepositoryMethod(config, entity, action, isCommand);
        AddApplicationFiles(config, entity, action, isCommand);
        AddControllerMethod(config, entity, action, isCommand);

        Console.WriteLine($"Action {action} for {entity} generated.");
    }

    static void AddRepositoryMethod(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var iface = Path.Combine(config.SolutionPath, $"{solution}.Core", "Domain", plural, $"I{entity}Repository.cs");
        if (File.Exists(iface))
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
        var impl = Path.Combine(config.SolutionPath, $"{solution}.Infrastructure", plural, $"{entity}Repository.cs");
        if (File.Exists(impl))
        {
            var lines = File.ReadAllLines(impl).ToList();
            if (!lines.Any(l => l.Contains($"{Upper(action)}Async(")))
            {
                var idx = lines.FindLastIndex(l => l.Trim() == "}");
                var method = isCommand
                    ? new[]
                    {
                        $"    public async Task {Upper(action)}Async({entity} entity)",
                        "    {",
                        "        // TODO: implement action",
                        "        await Task.CompletedTask;",
                        "    }"
                    }
                    : new[]
                    {
                        $"    public async Task<{entity}?> {Upper(action)}Async(int id)",
                        "    {",
                        "        // TODO: implement action",
                        $"        return await _context.Set<{entity}>().FindAsync(id);",
                        "    }"
                    };
                lines.InsertRange(idx, method);
                File.WriteAllLines(impl, lines);
            }
        }
    }

    static void AddApplicationFiles(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var appBase = Path.Combine(config.SolutionPath, $"{solution}.Application", plural);
        var dir = Path.Combine(appBase, isCommand ? "Commands" : "Queries", Upper(action));
        Directory.CreateDirectory(dir);
        string Fill(string t) => t.Replace("{{solution}}", solution)
                                  .Replace("{{entity}}", entity)
                                  .Replace("{{entities}}", plural)
                                  .Replace("{{action}}", Upper(action));
        if (isCommand)
        {
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Command.cs"), Fill(@"
using MediatR;
using {{solution}}.Core.Domain.{{entities}};

namespace {{solution}}.Application.{{entities}}.Commands.{{action}};
public record {{action}}{{entity}}Command({{entity}} Entity) : IRequest;
"));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Handler.cs"), Fill(@"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Interfaces;
using {{solution}}.Core.Domain.{{entities}};

namespace {{solution}}.Application.{{entities}}.Commands.{{action}};
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
"));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Validator.cs"), Fill(@"
using FluentValidation;

namespace {{solution}}.Application.{{entities}}.Commands.{{action}};
public class {{action}}{{entity}}Validator : AbstractValidator<{{action}}{{entity}}Command>
{
    public {{action}}{{entity}}Validator()
    {
        RuleFor(x => x.Entity).NotNull();
    }
}
"));
        }
        else
        {
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Query.cs"), Fill(@"
using MediatR;
using {{solution}}.Core.Domain.{{entities}};

namespace {{solution}}.Application.{{entities}}.Queries.{{action}};
public record {{action}}{{entity}}Query(int Id) : IRequest<{{entity}}?>;
"));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Handler.cs"), Fill(@"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Interfaces;
using {{solution}}.Core.Domain.{{entities}};

namespace {{solution}}.Application.{{entities}}.Queries.{{action}};
public class {{action}}{{entity}}Handler : IRequestHandler<{{action}}{{entity}}Query, {{entity}}?>
{
    private readonly IUnitOfWork _uow;
    public {{action}}{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}?> Handle({{action}}{{entity}}Query request, CancellationToken ct)
        => await _uow.{{entity}}Repository.{{action}}Async(request.Id);
}
"));
            File.WriteAllText(Path.Combine(dir, $"{Upper(action)}{entity}Validator.cs"), Fill(@"
using FluentValidation;

namespace {{solution}}.Application.{{entities}}.Queries.{{action}};
public class {{action}}{{entity}}Validator : AbstractValidator<{{action}}{{entity}}Query>
{
    public {{action}}{{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"));
        }
    }

    static void AddControllerMethod(SolutionConfig config, string entity, string action, bool isCommand)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(config.SolutionPath, config.StartupProject, plural);
        var file = Path.Combine(apiDir, $"{entity}Controller.cs");
        if (!File.Exists(file)) return;
        var lines = File.ReadAllLines(file).ToList();
        var usingLine = $"using {solution}.Application.{plural}.{(isCommand ? "Commands" : "Queries")}.{Upper(action)};";
        var lastUsing = lines.FindLastIndex(l => l.StartsWith("using "));
        if (!lines.Contains(usingLine))
            lines.Insert(lastUsing + 1, usingLine);
        var method = isCommand
            ? new[]
            {
                $"    [HttpPost(\"{action.ToLower()}\")]",
                $"    public async Task<IActionResult> {Upper(action)}([FromBody] {entity} entity)",
                "    {",
                $"        await _mediator.Send(new {Upper(action)}{entity}Command(entity));",
                "        return Ok();",
                "    }",
                ""
            }
            : new[]
            {
                $"    [HttpGet(\"{action.ToLower()}/{{id}}\")]",
                $"    public async Task<{entity}?> {Upper(action)}(int id)",
                "        => await _mediator.Send(new " + Upper(action) + entity + "Query(id));",
                ""
            };
        var end = lines.FindLastIndex(l => l.Trim() == "}");
        lines.InsertRange(end, method);
        File.WriteAllLines(file, lines);
    }

    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
}
