using System;
using IdentityService.Application.Features.Roles.Dtos;

namespace IdentityService.Application.Features.UserRoles.Dtos;

public sealed record UserRoleRequest(Guid RoleId);

public sealed record UserRoleDto(RoleListItemDto Role);
