using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Common.Responses;
using IdentityService.Application.Features.Scopes.Commands.CreateScope;
using IdentityService.Application.Features.Scopes.Commands.DeleteScope;
using IdentityService.Application.Features.Scopes.Commands.UpdateScope;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Application.Features.Scopes.Queries.GetScopeById;
using IdentityService.Application.Features.Scopes.Queries.GetScopesPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Scopes")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class ScopesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ScopeListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ScopeListItemDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetScopesPagedQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ScopeDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScopeDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetScopeByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedIdResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedIdResponse>> Create([FromBody] CreateScopeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateScopeCommand(request), ct);
        var payload = new CreatedIdResponse(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, payload);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScopeRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new UpdateScopeCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteScopeCommand(id), ct);
        return NoContent();
    }
}
