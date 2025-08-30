using System.IO;

namespace DotNetArch.Scaffolding.Steps;

public class ControllerStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        var apiDir = Path.Combine(basePath, startupProject, entity);
        Directory.CreateDirectory(apiDir);
        var controllerFile = Path.Combine(apiDir, $"{entity}Controller.cs");
        var lower = entity.ToLowerInvariant();
        var content = @"using MediatR;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using {{solution}}.Application.{{entity}}.Commands.Create;
using {{solution}}.Application.{{entity}}.Commands.Update;
using {{solution}}.Application.{{entity}}.Commands.Delete;
using {{solution}}.Application.{{entity}}.Queries.GetById;
using {{solution}}.Application.{{entity}}.Queries.GetAll;
using {{solution}}.Application.{{entity}}.Queries.GetList;
using {{solution}}.Core.Common;
using {{solution}}.Core.Domain.{{entity}};

namespace {{startupProject}}.{{entity}};

[ApiController]
[Route("api/[controller]")]
public class {{entity}}Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public {{entity}}Controller(IMediator mediator) => _mediator = mediator;

    [HttpGet("{{lower}}/{id}")]
    public async Task<{{entity}}> GetById(int id) => await _mediator.Send(new Get{{entity}}ByIdQuery(id));

    [HttpGet("all")]
    public async Task<List<{{entity}}>> GetAll() => await _mediator.Send(new Get{{entity}}AllQuery());

    [HttpGet]
    public async Task<PagedResult<{{entity}}>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => await _mediator.Send(new Get{{entity}}ListQuery(page, pageSize));

    [HttpPost]
    public async Task<{{entity}}> Create({{entity}} entity) => await _mediator.Send(new Create{{entity}}Command(entity));

    [HttpPut]
    public async Task Update({{entity}} entity) => await _mediator.Send(new Update{{entity}}Command(entity));

    [HttpDelete("{{lower}}/{id}")]
    public async Task Delete(int id) => await _mediator.Send(new Delete{{entity}}Command(id));
}
";
        content = content
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{startupProject}}", startupProject)
            .Replace("{{lower}}", lower);
        File.WriteAllText(controllerFile, content);
    }
}
