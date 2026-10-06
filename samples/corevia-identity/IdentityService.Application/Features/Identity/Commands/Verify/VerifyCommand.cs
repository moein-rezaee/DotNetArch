using IdentityService.Application.Features.Identity.Models;
using IdentityService.Domain.Entities;
using MediatR;

namespace IdentityService.Application.Features.Identity.Commands.Verify;

public record VerifyCommand(VerifyCodeRequest Request, string? IpAddress, string? DeviceInfo)
    : IRequest<(User User, string AccessToken, string RefreshToken)>;
