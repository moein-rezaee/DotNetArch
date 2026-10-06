using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Common.Responses;
using IdentityService.Application.Features.Permissions.Commands.CreatePermission;
using IdentityService.Application.Features.Permissions.Commands.DeletePermission;
using IdentityService.Application.Features.Permissions.Commands.UpdatePermission;
using IdentityService.Application.Features.Permissions.Queries.GetPermissionById;
using IdentityService.Application.Features.Permissions.Queries.GetPermissionsPaged;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Permissions")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class PermissionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PermissionDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPermissionsPagedQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPermissionByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedIdResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedIdResponse>> Create([FromBody] CreatePermissionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePermissionCommand(request), ct);
        var payload = new CreatedIdResponse(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, payload);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePermissionRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new UpdatePermissionCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        await _mediator.Send(new DeletePermissionCommand(id, reason), ct);
        return NoContent();
    }
}
