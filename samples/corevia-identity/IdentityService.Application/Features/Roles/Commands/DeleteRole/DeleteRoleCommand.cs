using System;
using MediatR;

namespace IdentityService.Application.Features.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id) : IRequest;

