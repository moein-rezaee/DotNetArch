using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class ControllerStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var startupProject = config.StartupProject;
        var noDb = string.Equals(config.DatabaseProvider, "None", StringComparison.OrdinalIgnoreCase);
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(basePath, startupProject, "Features", plural);
        Directory.CreateDirectory(apiDir);
        var controllerFile = Path.Combine(apiDir, $"{entity}Controller.cs");
        var content = noDb ? """
using MediatR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using {{solution}}.Application.Features.{{entities}}.Commands.Create;
using {{solution}}.Application.Features.{{entities}}.Commands.Update;
using {{solution}}.Application.Features.{{entities}}.Commands.Delete;
using {{solution}}.Application.Features.{{entities}}.Queries.GetById;
using {{solution}}.Application.Features.{{entities}}.Queries.GetAll;

namespace {{startupProject}}.Features.{{entities}};

[ApiController]
[Route("Api/[controller]")]
public class {{entity}}Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public {{entity}}Controller(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => await _mediator.Send(new Get{{entity}}ByIdQuery(id)) is object result ? Ok(result) : NotFound();

    [HttpGet("All")]
    public async Task<IActionResult> GetAll()
        => Ok(await _mediator.Send(new Get{{entity}}AllQuery()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Create{{entity}}Command command)
    {
        await _mediator.Send(command);
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Update{{entity}}Command command)
    {
        await _mediator.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new Delete{{entity}}Command(id));
        return NoContent();
    }
}
""" : """
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
using {{solution}}.Core.Common.Models;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{startupProject}}.Features.{{entities}};

[ApiController]
[Route("Api/[controller]")]
public class {{entity}}Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public {{entity}}Controller(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new Get{{entity}}ByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("All")]
    public async Task<List<{{entity}}Model>> GetAll() => await _mediator.Send(new Get{{entity}}AllQuery());

    [HttpGet("List")]
    public async Task<PagedResult<{{entity}}Model>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => await _mediator.Send(new Get{{entity}}ListQuery(page, pageSize));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Create{{entity}}Command command)
    {
        await _mediator.Send(command);
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task Update(int id, [FromBody] Update{{entity}}Command command)
    {
        await _mediator.Send(command with { Id = id });
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
            // Remove any lingering Entity namespace usings from older templates
            var entityUsing = $"using {solution}.Core.Features.{plural}.Entities;";
            if (text.Contains(entityUsing))
                text = text.Replace(entityUsing + "\n", string.Empty).Replace("\n" + entityUsing, string.Empty);
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
                {"GetById", noDb
                    ? "    [HttpGet(\"{id}\")]\n    public async Task<IActionResult> GetById(int id)\n        => await _mediator.Send(new Get"+entity+"ByIdQuery(id)) is object result ? Ok(result) : NotFound();\n"
                    : "    [HttpGet(\"{id}\")]\n    public async Task<IActionResult> GetById(int id)\n    {\n        var result = await _mediator.Send(new Get"+entity+"ByIdQuery(id));\n        return result is null ? NotFound() : Ok(result);\n    }\n"},
                {"GetAll", noDb
                    ? "    [HttpGet(\"All\")]\n    public async Task<IActionResult> GetAll()\n        => Ok(await _mediator.Send(new Get"+entity+"AllQuery()));\n"
                    : "    [HttpGet(\"All\")]\n    public async Task<List<"+entity+"Model>> GetAll() => await _mediator.Send(new Get"+entity+"AllQuery());\n"},
                {"Create", noDb
                    ? "    [HttpPost]\n    public async Task<IActionResult> Create([FromBody] Create"+entity+"Command command)\n    {\n        await _mediator.Send(command);\n        return Ok();\n    }\n"
                    : "    [HttpPost]\n    public async Task<IActionResult> Create([FromBody] Create"+entity+"Command command)\n    {\n        var created = await _mediator.Send(command);\n        return Ok(created);\n    }\n"},
                {"Update", noDb
                    ? "    [HttpPut(\"{id}\")]\n    public async Task<IActionResult> Update(int id, [FromBody] Update"+entity+"Command command)\n    {\n        await _mediator.Send(command with { Id = id });\n        return NoContent();\n    }\n"
                    : "    [HttpPut(\"{id}\")]\n    public async Task Update(int id, [FromBody] Update"+entity+"Command command)\n    {\n        command.Entity.Id = id;\n        await _mediator.Send(command);\n    }\n"},
                {"Delete", noDb
                    ? "    [HttpDelete(\"{id}\")]\n    public async Task<IActionResult> Delete(int id)\n    {\n        await _mediator.Send(new Delete"+entity+"Command(id));\n        return NoContent();\n    }\n"
                    : "    [HttpDelete(\"{id}\")]\n    public async Task Delete(int id) => await _mediator.Send(new Delete"+entity+"Command(id));\n"}
            };
            bool HasMethod(string src, string name)
            {
                var pattern = @"\bpublic\s+async\s+Task(?:<[^>]+>)?\s+" + Regex.Escape(name) + @"\s*\(";
                return Regex.IsMatch(src, pattern);
            }
            foreach (var kv in methods)
            {
                if (noDb && kv.Key == "GetList") continue;
                if (!HasMethod(text, kv.Key))
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
                $"using {solution}.Application.Features.{plural}.Models;"
            };
            foreach (var u in requiredUsings)
                if (!text.Contains(u))
                    text = u + Environment.NewLine + text;
            if (!noDb)
            {
                if (!text.Contains($"using {solution}.Application.Features.{plural}.Queries.GetList;"))
                    text = $"using {solution}.Application.Features.{plural}.Queries.GetList;\n" + text;
                if (!text.Contains($"using {solution}.Core.Common.Models;"))
                    text = $"using {solution}.Core.Common.Models;\n" + text;
            }
            File.WriteAllText(controllerFile, text);
        }
    }
}
