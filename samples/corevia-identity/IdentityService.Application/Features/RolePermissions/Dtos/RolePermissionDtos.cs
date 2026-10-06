using System;
using IdentityService.Application.Features.Roles.Dtos;

namespace IdentityService.Application.Features.RolePermissions.Dtos;

public sealed record RolePermissionRequest(Guid PermissionId);

public sealed record RolePermissionDto(PermissionDto Permission);
