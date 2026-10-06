using System;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Roles.Commands.UpdateRole;

public sealed record UpdateRoleRequest(string Name, string DisplayName, string? Description);

public sealed record UpdateRoleCommand(Guid Id, UpdateRoleRequest Request) : IRequest<RoleDetailDto>;
