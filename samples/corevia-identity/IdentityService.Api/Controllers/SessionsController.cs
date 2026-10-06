using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Sessions.Commands.RevokeOtherSessions;
using IdentityService.Application.Features.Sessions.Commands.RevokeSession;
using IdentityService.Application.Features.Sessions.Dtos;
using IdentityService.Application.Features.Sessions.Queries.GetCurrentUserSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Sessions")]
[Authorize(Policy = "IdentitySelf")]
[Produces("application/json")]
public class SessionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SessionDto>>> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetCurrentUserSessionsQuery(userId), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _mediator.Send(new RevokeSessionCommand(userId, id), ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> RevokeOthers(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var sessionId = GetCurrentSessionId();
        await _mediator.Send(new RevokeOtherSessionsCommand(userId, sessionId), ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedException("User identifier is missing or invalid.", "missing_user_id");
        }

        return userId;
    }

    private Guid? GetCurrentSessionId()
    {
        var sid = User.FindFirstValue("sid");
        if (sid is null)
        {
            return null;
        }

        return Guid.TryParse(sid, out var id) ? id : null;
    }
}
