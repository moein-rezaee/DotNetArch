using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.UserRoles.Commands.AddUserRole;
using IdentityService.Application.Features.UserRoles.Commands.RemoveUserRole;
using IdentityService.Application.Features.UserRoles.Dtos;
using IdentityService.Application.Features.UserRoles.Queries.GetUserRoles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Users/{userId:guid}/Roles")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class UserRolesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserRoleDto>>> Get(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserRolesQuery(userId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Add(Guid userId, [FromBody] UserRoleRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new AddUserRoleCommand(userId, request), ct);
        return Ok();
    }

    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid userId, Guid roleId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveUserRoleCommand(userId, roleId), ct);
        return NoContent();
    }
}
