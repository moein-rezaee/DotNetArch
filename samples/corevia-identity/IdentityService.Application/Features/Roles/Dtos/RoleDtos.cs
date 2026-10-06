using System;
using System.Collections.Generic;

namespace IdentityService.Application.Features.Roles.Dtos;

public sealed record RoleListItemDto(Guid Id, string Name, string DisplayName, string? Description);

public sealed record PermissionDto(Guid Id, string Key, string DisplayName, string? Description);

public sealed record RoleDetailDto(Guid Id, string Name, string DisplayName, string? Description, IReadOnlyCollection<PermissionDto> Permissions);

