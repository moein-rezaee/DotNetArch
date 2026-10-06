using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Profile.Commands.UpdateProfile;
using IdentityService.Application.Features.Profile.Models;
using IdentityService.Application.Features.Profile.Queries.GetProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Profile")]
[Authorize(Policy = "IdentitySelf")]
[Produces("application/json")]
public class ProfileController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<UserProfileResponse>> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var profile = await _mediator.Send(new GetProfileQuery(userId), ct);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _mediator.Send(new UpdateProfileCommand(userId, request), ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedException("User identifier is missing or invalid.", "missing_user_id");
        }

        return userId;
    }
}
