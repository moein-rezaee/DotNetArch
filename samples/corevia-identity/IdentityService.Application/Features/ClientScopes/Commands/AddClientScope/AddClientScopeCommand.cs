using System;
using System.Collections.Generic;
using IdentityService.Application.Features.ClientScopes.Dtos;
using MediatR;

namespace IdentityService.Application.Features.ClientScopes.Commands.AddClientScope;

public sealed record AddClientScopeCommand(Guid ClientId, ClientScopeRequest Request) : IRequest<IReadOnlyCollection<ClientScopeDto>>;

