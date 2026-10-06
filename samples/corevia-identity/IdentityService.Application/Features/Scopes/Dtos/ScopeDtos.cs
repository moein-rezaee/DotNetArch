using System;
using System.Collections.Generic;
using IdentityService.Application.Features.Roles.Dtos;

namespace IdentityService.Application.Features.Scopes.Dtos;

public sealed record ScopeListItemDto(Guid Id, string Name, string DisplayName, string? Description);

public sealed record ScopeDetailDto(Guid Id, string Name, string DisplayName, string? Description, IReadOnlyCollection<PermissionDto> Permissions);

