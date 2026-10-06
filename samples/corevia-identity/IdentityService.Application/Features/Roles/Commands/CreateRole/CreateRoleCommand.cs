using System;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Roles.Commands.CreateRole;

public sealed record CreateRoleRequest(string Name, string DisplayName, string? Description);

public sealed record CreateRoleCommand(CreateRoleRequest Request) : IRequest<RoleDetailDto>;
