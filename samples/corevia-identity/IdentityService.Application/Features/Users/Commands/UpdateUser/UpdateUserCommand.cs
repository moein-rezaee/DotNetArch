using System;
using IdentityService.Application.Features.Users.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Users.Commands.UpdateUser;

public sealed record UpdateUserRequest(string PhoneNumber, bool IsActive);

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IRequest<UserDetailDto>;

