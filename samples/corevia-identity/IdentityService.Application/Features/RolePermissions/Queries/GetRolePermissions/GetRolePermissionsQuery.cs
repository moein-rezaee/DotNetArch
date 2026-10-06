using System;
using System.Collections.Generic;
using IdentityService.Application.Features.RolePermissions.Dtos;
using MediatR;

namespace IdentityService.Application.Features.RolePermissions.Queries.GetRolePermissions;

public sealed record GetRolePermissionsQuery(Guid RoleId) : IRequest<IReadOnlyCollection<RolePermissionDto>>;

