using IdentityService.Application.Features.Identity.Models;
using IdentityService.Domain.Entities;
using MediatR;

namespace IdentityService.Application.Features.Identity.Commands.Refresh;

public record RefreshCommand(RefreshRequest Request) : IRequest<(User User, string AccessToken, string RefreshToken)>;

