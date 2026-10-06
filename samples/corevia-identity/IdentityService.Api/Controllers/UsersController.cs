using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Common.Responses;
using IdentityService.Application.Features.Users.Commands.CreateUser;
using IdentityService.Application.Features.Users.Commands.DeleteUser;
using IdentityService.Application.Features.Users.Commands.UpdateUser;
using IdentityService.Application.Features.Users.Dtos;
using IdentityService.Application.Features.Users.Queries.GetUserById;
using IdentityService.Application.Features.Users.Queries.GetUsersPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Users")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class UsersController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<UserListItemDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? phone = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? roleId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUsersPagedQuery(pageNumber, pageSize, phone, tenantId, roleId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedIdResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedIdResponse>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateUserCommand(request), ct);
        var payload = new CreatedIdResponse(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, payload);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new UpdateUserCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteUserCommand(id), ct);
        return NoContent();
    }
}
