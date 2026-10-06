using System;
using IdentityService.Application.Features.Tenants.Dtos;

namespace IdentityService.Application.Features.UserTenants.Dtos;

public sealed record UserTenantRequest(Guid TenantId, bool IsDefault);

public sealed record UserTenantDto(TenantListItemDto Tenant, bool IsDefault);
