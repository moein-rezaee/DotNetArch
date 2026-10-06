using System;
using MediatR;

namespace IdentityService.Application.Features.UserRoles.Commands.RemoveUserRole;

public sealed record RemoveUserRoleCommand(Guid UserId, Guid RoleId) : IRequest;

