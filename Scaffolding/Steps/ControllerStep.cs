using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class ControllerStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(basePath, startupProject, plural);
        Directory.CreateDirectory(apiDir);
        var controllerFile = Path.Combine(apiDir, $"{entity}Controller.cs");
        var content =
"using MediatR\n" +
"using System.Collections.Generic;\n" +
"using Microsoft.AspNetCore.Mvc;\n" +
"using {{solution}}.Application.{{entities}}.Commands.Create;\n" +
"using {{solution}}.Application.{{entities}}.Commands.Update;\n" +
"using {{solution}}.Application.{{entities}}.Commands.Delete;\n" +
"using {{solution}}.Application.{{entities}}.Queries.GetById;\n" +
"using {{solution}}.Application.{{entities}}.Queries.GetAll;\n" +
"using {{solution}}.Application.{{entities}}.Queries.GetList;\n" +
"using {{solution}}.Core.Common;\n" +
"using {{solution}}.Core.Domain.{{entities}};\n\n" +
"namespace {{startupProject}}.{{entities}};\n\n" +
"[ApiController]\n" +
"[Route(\"api/[controller]\")]\n" +
"public class {{entity}}Controller : ControllerBase\n" +
"{\n" +
"    private readonly IMediator _mediator;\n\n" +
"    public {{entity}}Controller(IMediator mediator) => _mediator = mediator;\n\n" +
"    [HttpGet(\"{id}\")]\n" +
"    public async Task<{{entity}}?> GetById(int id) => await _mediator.Send(new Get{{entity}}ByIdQuery(id));\n\n" +
"    [HttpGet(\"all\")]\n" +
"    public async Task<List<{{entity}}>> GetAll() => await _mediator.Send(new Get{{entity}}AllQuery());\n\n" +
"    [HttpGet]\n" +
"    public async Task<PagedResult<{{entity}}>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)\n" +
"        => await _mediator.Send(new Get{{entity}}ListQuery(page, pageSize));\n\n" +
"    [HttpPost]\n" +
"    public async Task<{{entity}}> Create([FromBody] {{entity}} entity) => await _mediator.Send(new Create{{entity}}Command(entity));\n\n" +
"    [HttpPut(\"{id}\")]\n" +
"    public async Task Update(int id, [FromBody] {{entity}} entity)\n" +
"    {\n" +
"        entity.Id = id;\n" +
"        await _mediator.Send(new Update{{entity}}Command(entity));\n" +
"    }\n\n" +
"    [HttpDelete(\"{id}\")]\n" +
"    public async Task Delete(int id) => await _mediator.Send(new Delete{{entity}}Command(id));\n" +
"}\n";
        content = content
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{entities}}", plural)
            .Replace("{{startupProject}}", startupProject);
        File.WriteAllText(controllerFile, content);
    }
}
