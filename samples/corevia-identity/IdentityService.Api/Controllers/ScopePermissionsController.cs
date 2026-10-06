using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.ScopePermissions.Commands.AddScopePermission;
using IdentityService.Application.Features.ScopePermissions.Commands.RemoveScopePermission;
using IdentityService.Application.Features.ScopePermissions.Dtos;
using IdentityService.Application.Features.ScopePermissions.Queries.GetScopePermissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Scopes/{scopeId:guid}/Permissions")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class ScopePermissionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ScopePermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ScopePermissionDto>>> Get(Guid scopeId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetScopePermissionsQuery(scopeId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Add(Guid scopeId, [FromBody] ScopePermissionRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new AddScopePermissionCommand(scopeId, request), ct);
        return Ok();
    }

    [HttpDelete("{permissionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid scopeId, Guid permissionId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveScopePermissionCommand(scopeId, permissionId), ct);
        return NoContent();
    }
}
