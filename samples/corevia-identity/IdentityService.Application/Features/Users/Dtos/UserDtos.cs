using System;
using System.Collections.Generic;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.Tenants.Dtos;

namespace IdentityService.Application.Features.Users.Dtos;

public sealed record UserListItemDto(Guid Id, string PhoneNumber, DateTime CreatedAt, DateTime? UpdatedAt, bool IsActive);

public sealed record UserDetailDto(
    Guid Id,
    string PhoneNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive,
    IReadOnlyCollection<RoleListItemDto> Roles,
    IReadOnlyCollection<TenantListItemDto> Tenants);
