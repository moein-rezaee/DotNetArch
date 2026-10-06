using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Users.Dtos;
using IdentityService.Application.Features.Users.Queries.GetUsersPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Roles/{roleId:guid}/Users")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class RoleUsersController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<UserListItemDto>>> Get(
        Guid roleId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetUsersPagedQuery(pageNumber, pageSize, null, null, roleId),
            ct);

        return Ok(result);
    }
}

