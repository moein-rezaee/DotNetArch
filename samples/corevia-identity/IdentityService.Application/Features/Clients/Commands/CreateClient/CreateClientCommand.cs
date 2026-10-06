using System;
using IdentityService.Application.Features.Clients.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.CreateClient;

public sealed record CreateClientRequest(string Name, string? Description, bool IsActive);

public sealed record CreateClientCommand(CreateClientRequest Request) : IRequest<ClientDetailDto>;
