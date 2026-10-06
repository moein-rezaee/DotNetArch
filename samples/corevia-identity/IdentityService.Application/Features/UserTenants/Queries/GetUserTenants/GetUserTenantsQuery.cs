using System;
using System.Collections.Generic;
using IdentityService.Application.Features.UserTenants.Dtos;
using MediatR;

namespace IdentityService.Application.Features.UserTenants.Queries.GetUserTenants;

public sealed record GetUserTenantsQuery(Guid UserId) : IRequest<IReadOnlyCollection<UserTenantDto>>;

