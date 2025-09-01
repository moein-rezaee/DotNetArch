using System;
using System.Collections.Generic;
using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class ControllerStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var startupProject = config.StartupProject;
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(basePath, startupProject, "Features", plural);
        Directory.CreateDirectory(apiDir);
        var controllerFile = Path.Combine(apiDir, $"{entity}Controller.cs");
        var content = """
using MediatR;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using {{solution}}.Application.Features.{{entities}}.Commands.Create;
using {{solution}}.Application.Features.{{entities}}.Commands.Update;
using {{solution}}.Application.Features.{{entities}}.Commands.Delete;
using {{solution}}.Application.Features.{{entities}}.Queries.GetById;
using {{solution}}.Application.Features.{{entities}}.Queries.GetAll;
using {{solution}}.Application.Features.{{entities}}.Queries.GetList;
using {{solution}}.Core.Models;
using {{solution}}.Core.Features.{{entities}};

namespace {{startupProject}}.Features.{{entities}};

[ApiController]
[Route("Api/[controller]")]
public class {{entity}}Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public {{entity}}Controller(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<{{entity}}?> GetById(int id) => await _mediator.Send(new Get{{entity}}ByIdQuery(id));

    [HttpGet("All")]
    public async Task<List<{{entity}}>> GetAll() => await _mediator.Send(new Get{{entity}}AllQuery());

    [HttpGet("List")]
    public async Task<PagedResult<{{entity}}>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => await _mediator.Send(new Get{{entity}}ListQuery(page, pageSize));

    [HttpPost]
    public async Task<{{entity}}> Create([FromBody] {{entity}} entity) =>
        await _mediator.Send(new Create{{entity}}Command(entity));

    [HttpPut("{id}")]
    public async Task Update(int id, [FromBody] {{entity}} entity)
    {
        entity.Id = id;
        await _mediator.Send(new Update{{entity}}Command(entity));
    }

    [HttpDelete("{id}")]
    public async Task Delete(int id) => await _mediator.Send(new Delete{{entity}}Command(id));
}
""";
        content = content
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{entities}}", plural)
            .Replace("{{startupProject}}", startupProject);
        if (!File.Exists(controllerFile))
        {
            File.WriteAllText(controllerFile, content);
        }
        else
        {
            var text = File.ReadAllText(controllerFile);
            if (!text.Contains("IMediator _mediator"))
            {
                var classIdx = text.IndexOf("{", text.IndexOf("class", StringComparison.Ordinal));
                text = text.Insert(classIdx + 1, "\n    private readonly IMediator _mediator;\n");
            }
            if (!text.Contains($"public {entity}Controller(IMediator mediator)"))
            {
                var classIdx = text.IndexOf("{", text.IndexOf("class", StringComparison.Ordinal));
                var ctor = $"\n    public {entity}Controller(IMediator mediator) => _mediator = mediator;\n";
                text = text.Insert(classIdx + 1, ctor);
            }
            var methods = new Dictionary<string,string>
            {
                {"GetById", "    [HttpGet(\"{id}\")]\n    public async Task<"+entity+"?> GetById(int id) => await _mediator.Send(new Get"+entity+"ByIdQuery(id));\n"},
                {"GetAll", "    [HttpGet(\"All\")]\n    public async Task<List<"+entity+">> GetAll() => await _mediator.Send(new Get"+entity+"AllQuery());\n"},
                {"GetList", "    [HttpGet(\"List\")]\n    public async Task<PagedResult<"+entity+">> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)\n        => await _mediator.Send(new Get"+entity+"ListQuery(page, pageSize));\n"},
                {"Create", "    [HttpPost]\n    public async Task<"+entity+"> Create([FromBody] "+entity+" entity) =>\n        await _mediator.Send(new Create"+entity+"Command(entity));\n"},
                {"Update", "    [HttpPut(\"{id}\")]\n    public async Task Update(int id, [FromBody] "+entity+" entity)\n    {\n        entity.Id = id;\n        await _mediator.Send(new Update"+entity+"Command(entity));\n    }\n"},
                {"Delete", "    [HttpDelete(\"{id}\")]\n    public async Task Delete(int id) => await _mediator.Send(new Delete"+entity+"Command(id));\n"}
            };
            foreach (var kv in methods)
            {
                if (!text.Contains(kv.Key))
                {
                    var idx = text.LastIndexOf("}");
                    text = text.Insert(idx, kv.Value);
                }
            }
            var requiredUsings = new[]
            {
                "using MediatR;",
                "using System.Collections.Generic;",
                "using System.Threading.Tasks;",
                "using Microsoft.AspNetCore.Mvc;",
                $"using {solution}.Application.Features.{plural}.Commands.Create;",
                $"using {solution}.Application.Features.{plural}.Commands.Update;",
                $"using {solution}.Application.Features.{plural}.Commands.Delete;",
                $"using {solution}.Application.Features.{plural}.Queries.GetById;",
                $"using {solution}.Application.Features.{plural}.Queries.GetAll;",
                $"using {solution}.Application.Features.{plural}.Queries.GetList;",
                $"using {solution}.Core.Models;",
                $"using {solution}.Core.Features.{plural};"
            };
            foreach (var u in requiredUsings)
                if (!text.Contains(u))
                    text = u + Environment.NewLine + text;
            File.WriteAllText(controllerFile, text);
        }
    }
}

