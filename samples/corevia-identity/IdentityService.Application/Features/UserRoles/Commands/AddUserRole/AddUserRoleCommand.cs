using System;
using System.Collections.Generic;
using IdentityService.Application.Features.UserRoles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.UserRoles.Commands.AddUserRole;

public sealed record AddUserRoleCommand(Guid UserId, UserRoleRequest Request) : IRequest<IReadOnlyCollection<UserRoleDto>>;

