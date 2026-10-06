using System;
using IdentityService.Application.Features.Users.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;

