using System;
using IdentityService.Application.Features.Profile.Models;
using MediatR;

namespace IdentityService.Application.Features.Profile.Queries.GetProfile;

public sealed record GetProfileQuery(Guid UserId) : IRequest<UserProfileResponse>;

