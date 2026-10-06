using IdentityService.Application.Features.Identity.Commands.Logout;
using IdentityService.Application.Features.Identity.Commands.Refresh;
using IdentityService.Application.Features.Identity.Commands.Send;
using IdentityService.Application.Features.Identity.Commands.Verify;
using IdentityService.Application.Features.Identity.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Identity")]
[Produces("application/json")]
public class IdentityController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [AllowAnonymous]
    [HttpPost("Send")]
    public async Task<IActionResult> Send([FromBody] SendCodeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SendCommand(request), ct);
        return Accepted(new SendCodeResponse(Accepted: true, Delivery: "queued"));
    }

    [AllowAnonymous]
    [HttpPost("Verify")]
    public async Task<ActionResult<TokenResponse>> Verify([FromBody] VerifyCodeRequest request, CancellationToken ct)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceInfo = Request.Headers.UserAgent.ToString();
        var (_, access, refresh) = await _mediator.Send(new VerifyCommand(request, remoteIp, deviceInfo), ct);
        return Ok(new TokenResponse(access, refresh));
    }

    [AllowAnonymous]
    [HttpPost("Refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var (user, access, refresh) = await _mediator.Send(new RefreshCommand(request), ct);
        return Ok(new TokenResponse(access, refresh));
    }

    [AllowAnonymous]
    [HttpPost("Logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(request), ct);
        return Ok();
    }
}
