using System;
using IdentityService.Application.Features.Tenants.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Commands.UpdateTenant;

public sealed record UpdateTenantRequest(string Name, string DisplayName, bool IsActive);

public sealed record UpdateTenantCommand(Guid Id, UpdateTenantRequest Request) : IRequest<TenantDetailDto>;

