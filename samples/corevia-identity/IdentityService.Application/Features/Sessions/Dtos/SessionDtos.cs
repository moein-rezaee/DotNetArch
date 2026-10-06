using System;

namespace IdentityService.Application.Features.Sessions.Dtos;

public sealed record SessionDto(Guid Id, DateTime CreatedAt, DateTime? EndedAt, string? DeviceInfo, string? IpAddress, bool IsRevoked);

