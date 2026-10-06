using System;
using System.Collections.Generic;
using IdentityService.Application.Features.RolePermissions.Dtos;
using MediatR;

namespace IdentityService.Application.Features.RolePermissions.Commands.AddRolePermission;

public sealed record AddRolePermissionCommand(Guid RoleId, RolePermissionRequest Request) : IRequest<IReadOnlyCollection<RolePermissionDto>>;

