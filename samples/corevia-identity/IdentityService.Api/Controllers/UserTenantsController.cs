using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.UserTenants.Commands.AddUserTenant;
using IdentityService.Application.Features.UserTenants.Commands.RemoveUserTenant;
using IdentityService.Application.Features.UserTenants.Dtos;
using IdentityService.Application.Features.UserTenants.Queries.GetUserTenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Users/{userId:guid}/Tenants")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class UserTenantsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserTenantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserTenantDto>>> Get(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserTenantsQuery(userId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Add(Guid userId, [FromBody] UserTenantRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new AddUserTenantCommand(userId, request), ct);
        return Ok();
    }

    [HttpDelete("{tenantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid userId, Guid tenantId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveUserTenantCommand(userId, tenantId), ct);
        return NoContent();
    }
}
