using System;
using IdentityService.Application.Features.Clients.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.UpdateClient;

public sealed record UpdateClientRequest(string Name, string? Description, bool IsActive);

public sealed record UpdateClientCommand(Guid Id, UpdateClientRequest Request) : IRequest<ClientDetailDto>;
