using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Commands.CreatePermission;

public sealed record CreatePermissionRequest(string Key, string DisplayName, string? Description);

public sealed record CreatePermissionCommand(CreatePermissionRequest Request) : IRequest<PermissionDto>;

