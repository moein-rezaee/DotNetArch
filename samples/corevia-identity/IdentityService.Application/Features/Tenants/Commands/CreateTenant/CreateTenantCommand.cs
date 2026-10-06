using IdentityService.Application.Features.Tenants.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Commands.CreateTenant;

public sealed record CreateTenantRequest(string Name, string DisplayName, bool IsActive);

public sealed record CreateTenantCommand(CreateTenantRequest Request) : IRequest<TenantDetailDto>;

