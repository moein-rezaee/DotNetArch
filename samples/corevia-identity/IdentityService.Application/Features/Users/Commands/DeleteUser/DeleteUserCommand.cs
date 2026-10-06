using System;
using MediatR;

namespace IdentityService.Application.Features.Users.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid Id) : IRequest;

