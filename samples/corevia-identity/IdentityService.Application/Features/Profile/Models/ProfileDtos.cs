using System;

namespace IdentityService.Application.Features.Profile.Models;

public sealed record UserProfileResponse(
    Guid Id,
    string PhoneNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? FirstName,
    string? LastName,
    string? Email,
    string? AvatarUrl);

public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? AvatarUrl);
