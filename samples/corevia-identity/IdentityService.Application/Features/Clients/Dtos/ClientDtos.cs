using System;
using System.Collections.Generic;
using IdentityService.Application.Features.Scopes.Dtos;

namespace IdentityService.Application.Features.Clients.Dtos;

public sealed record ClientListItemDto(Guid Id, string ClientId, string Name, string? Description, bool IsActive);

public sealed record ClientDetailDto(
    Guid Id,
    string ClientId,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<ScopeListItemDto> Scopes);

public sealed record ClientSecretResponse(Guid Id, string Secret, DateTime CreatedAt, DateTime? ExpiresAt);

