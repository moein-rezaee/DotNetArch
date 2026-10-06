using System;
using System.Collections.Generic;
using IdentityService.Application.Features.UserRoles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.UserRoles.Queries.GetUserRoles;

public sealed record GetUserRolesQuery(Guid UserId) : IRequest<IReadOnlyCollection<UserRoleDto>>;

