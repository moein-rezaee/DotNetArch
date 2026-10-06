using System;
using MediatR;

namespace IdentityService.Application.Features.RolePermissions.Commands.RemoveRolePermission;

public sealed record RemoveRolePermissionCommand(Guid RoleId, Guid PermissionId) : IRequest;

