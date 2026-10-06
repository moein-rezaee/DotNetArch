using System;
using IdentityService.Application.Features.Scopes.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Queries.GetScopeById;

public sealed record GetScopeByIdQuery(Guid Id) : IRequest<ScopeDetailDto>;

