using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Queries.GetPermissionsPaged;

public sealed record GetPermissionsPagedQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<PermissionDto>>;

