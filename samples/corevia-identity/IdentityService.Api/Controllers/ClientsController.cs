using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Common.Responses;
using IdentityService.Application.Features.Clients.Commands.CreateClient;
using IdentityService.Application.Features.Clients.Commands.CreateClientSecret;
using IdentityService.Application.Features.Clients.Commands.RevokeClientSecret;
using IdentityService.Application.Features.Clients.Commands.UpdateClient;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Application.Features.Clients.Queries.GetClientById;
using IdentityService.Application.Features.Clients.Queries.GetClientsPaged;
using IdentityService.Application.Features.Clients.Queries.GetClientSecrets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Clients")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class ClientsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ClientListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ClientListItemDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetClientsPagedQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClientDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetClientByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedIdResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedIdResponse>> Create([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateClientCommand(request), ct);
        var payload = new CreatedIdResponse(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, payload);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new UpdateClientCommand(id, request), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/Secrets")]
    [ProducesResponseType(typeof(ClientSecretResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClientSecretResponse>> CreateSecret(Guid id, [FromBody] CreateClientSecretRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateClientSecretCommand(id, request), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/Secrets")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ClientSecretResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ClientSecretResponse>>> GetSecrets(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetClientSecretsQuery(id), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/Secrets/{secretId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSecret(Guid id, Guid secretId, CancellationToken ct)
    {
        await _mediator.Send(new RevokeClientSecretCommand(id, secretId), ct);
        return NoContent();
    }
}
