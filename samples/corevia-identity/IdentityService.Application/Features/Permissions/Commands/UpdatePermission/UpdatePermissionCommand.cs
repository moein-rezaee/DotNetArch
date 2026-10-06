using System;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Commands.UpdatePermission;

public sealed record UpdatePermissionRequest(string Key, string DisplayName, string? Description, bool IsDeprecated, string? DeprecationReason);

public sealed record UpdatePermissionCommand(Guid Id, UpdatePermissionRequest Request) : IRequest<PermissionDto>;

