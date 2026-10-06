using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.ClientScopes.Commands.AddClientScope;
using IdentityService.Application.Features.ClientScopes.Commands.RemoveClientScope;
using IdentityService.Application.Features.ClientScopes.Dtos;
using IdentityService.Application.Features.ClientScopes.Queries.GetClientScopes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Clients/{clientId:guid}/Scopes")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class ClientScopesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ClientScopeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ClientScopeDto>>> Get(Guid clientId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetClientScopesQuery(clientId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Add(Guid clientId, [FromBody] ClientScopeRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new AddClientScopeCommand(clientId, request), ct);
        return Ok();
    }

    [HttpDelete("{scopeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid clientId, Guid scopeId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveClientScopeCommand(clientId, scopeId), ct);
        return NoContent();
    }
}
