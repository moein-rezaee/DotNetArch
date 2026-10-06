using System;
using IdentityService.Application.Features.Profile.Models;
using MediatR;

namespace IdentityService.Application.Features.Profile.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, UpdateProfileRequest Request) : IRequest;

