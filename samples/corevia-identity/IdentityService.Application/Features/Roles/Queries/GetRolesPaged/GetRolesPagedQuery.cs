using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Roles.Queries.GetRolesPaged;

public sealed record GetRolesPagedQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<RoleListItemDto>>;

