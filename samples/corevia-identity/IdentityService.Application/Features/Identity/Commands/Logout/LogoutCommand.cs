using IdentityService.Application.Features.Identity.Models;
using MediatR;

namespace IdentityService.Application.Features.Identity.Commands.Logout;

public record LogoutCommand(LogoutRequest Request) : IRequest<Unit>;

