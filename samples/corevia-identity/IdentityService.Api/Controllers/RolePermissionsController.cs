using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.RolePermissions.Commands.AddRolePermission;
using IdentityService.Application.Features.RolePermissions.Commands.RemoveRolePermission;
using IdentityService.Application.Features.RolePermissions.Dtos;
using IdentityService.Application.Features.RolePermissions.Queries.GetRolePermissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Roles/{roleId:guid}/Permissions")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class RolePermissionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<RolePermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RolePermissionDto>>> Get(Guid roleId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRolePermissionsQuery(roleId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Add(Guid roleId, [FromBody] RolePermissionRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new AddRolePermissionCommand(roleId, request), ct);
        return Ok();
    }

    [HttpDelete("{permissionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveRolePermissionCommand(roleId, permissionId), ct);
        return NoContent();
    }
}
