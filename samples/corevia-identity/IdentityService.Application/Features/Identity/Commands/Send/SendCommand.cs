using IdentityService.Application.Features.Identity.Models;
using MediatR;

namespace IdentityService.Application.Features.Identity.Commands.Send;

public record SendCommand(SendCodeRequest Request) : IRequest<Unit>;

