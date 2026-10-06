using System;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Users.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Users.Queries.GetUsersPaged;

public sealed record GetUsersPagedQuery(
    int PageNumber,
    int PageSize,
    string? Phone,
    Guid? TenantId,
    Guid? RoleId) : IRequest<PagedResponse<UserListItemDto>>;
