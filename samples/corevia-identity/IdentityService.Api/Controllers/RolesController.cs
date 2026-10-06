using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Common.Responses;
using IdentityService.Application.Features.Roles.Commands.CreateRole;
using IdentityService.Application.Features.Roles.Commands.DeleteRole;
using IdentityService.Application.Features.Roles.Commands.UpdateRole;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.Roles.Queries.GetRoleById;
using IdentityService.Application.Features.Roles.Queries.GetRolesPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Roles")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class RolesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RoleListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RoleListItemDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRolesPagedQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoleDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoleByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedIdResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedIdResponse>> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateRoleCommand(request), ct);
        var payload = new CreatedIdResponse(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, payload);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new UpdateRoleCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteRoleCommand(id), ct);
        return NoContent();
    }
}
