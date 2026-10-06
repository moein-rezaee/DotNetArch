using {{App}}.Application.Common.Pagination;
using {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace {{App}}.Api.Controllers.{{Plural}};

[ApiController]
[Route("api/{{RouteName}}")]
[Produces("application/json")]
public sealed partial class {{Plural}}Controller(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<{{Entity}}Dto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<{{Entity}}Dto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new Get{{Plural}}Query(pageNumber, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof({{Entity}}Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<{{Entity}}Dto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new Get{{Entity}}ByIdQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof({{Entity}}Dto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<{{Entity}}Dto>> Create([FromBody] Create{{Entity}}Request request, CancellationToken cancellationToken)
    {
        var created = await sender.Send(new Create{{Entity}}Command(request.Name), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof({{Entity}}Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<{{Entity}}Dto>> Update(Guid id, [FromBody] Update{{Entity}}Request request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new Update{{Entity}}Command(id, request.Name), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new Delete{{Entity}}Command(id), cancellationToken);
        return NoContent();
    }
}
