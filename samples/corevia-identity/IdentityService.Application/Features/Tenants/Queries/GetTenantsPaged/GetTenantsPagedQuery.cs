using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Tenants.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Queries.GetTenantsPaged;

public sealed record GetTenantsPagedQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<TenantListItemDto>>;

