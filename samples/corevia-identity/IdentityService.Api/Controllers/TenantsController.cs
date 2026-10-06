using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Common.Responses;
using IdentityService.Application.Features.Tenants.Commands.CreateTenant;
using IdentityService.Application.Features.Tenants.Commands.DeleteTenant;
using IdentityService.Application.Features.Tenants.Commands.UpdateTenant;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Application.Features.Tenants.Queries.GetTenantById;
using IdentityService.Application.Features.Tenants.Queries.GetTenantsPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("v1/api/Tenants")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class TenantsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TenantListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TenantListItemDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTenantsPagedQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenantDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedIdResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedIdResponse>> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTenantCommand(request), ct);
        var payload = new CreatedIdResponse(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, payload);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        _ = await _mediator.Send(new UpdateTenantCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteTenantCommand(id), ct);
        return NoContent();
    }
}
