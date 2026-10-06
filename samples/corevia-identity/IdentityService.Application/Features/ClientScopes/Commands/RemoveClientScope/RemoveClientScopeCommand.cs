using System;
using MediatR;

namespace IdentityService.Application.Features.ClientScopes.Commands.RemoveClientScope;

public sealed record RemoveClientScopeCommand(Guid ClientId, Guid ScopeId) : IRequest;

