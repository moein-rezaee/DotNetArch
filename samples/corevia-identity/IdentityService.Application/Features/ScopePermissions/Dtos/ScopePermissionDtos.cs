using System;
using IdentityService.Application.Features.Roles.Dtos;

namespace IdentityService.Application.Features.ScopePermissions.Dtos;

public sealed record ScopePermissionRequest(Guid PermissionId);

public sealed record ScopePermissionDto(PermissionDto Permission);
