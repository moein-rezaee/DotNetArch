using System;
using IdentityService.Application.Features.Scopes.Dtos;

namespace IdentityService.Application.Features.ClientScopes.Dtos;

public sealed record ClientScopeRequest(Guid ScopeId);

public sealed record ClientScopeDto(ScopeListItemDto Scope);
