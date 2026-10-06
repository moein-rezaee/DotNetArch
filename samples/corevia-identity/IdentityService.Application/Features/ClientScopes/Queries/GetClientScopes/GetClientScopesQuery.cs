using System;
using System.Collections.Generic;
using IdentityService.Application.Features.ClientScopes.Dtos;
using MediatR;

namespace IdentityService.Application.Features.ClientScopes.Queries.GetClientScopes;

public sealed record GetClientScopesQuery(Guid ClientId) : IRequest<IReadOnlyCollection<ClientScopeDto>>;

