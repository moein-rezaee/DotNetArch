using IdentityService.Application.Features.Users.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Users.Commands.CreateUser;

public sealed record CreateUserRequest(string PhoneNumber, bool IsActive);

public sealed record CreateUserCommand(CreateUserRequest Request) : IRequest<UserDetailDto>;

