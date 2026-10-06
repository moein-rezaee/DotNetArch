using System;

namespace IdentityService.Application.Features.Tenants.Dtos;

public sealed record TenantListItemDto(Guid Id, string Name, string DisplayName, bool IsActive);

public sealed record TenantDetailDto(Guid Id, string Name, string DisplayName, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);

