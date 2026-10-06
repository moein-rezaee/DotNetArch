using System;
using IdentityService.Application.Features.Tenants.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Queries.GetTenantById;

public sealed record GetTenantByIdQuery(Guid Id) : IRequest<TenantDetailDto>;

