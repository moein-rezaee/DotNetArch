using System;
using IdentityService.Application.Features.Clients.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.CreateClientSecret;

public sealed record CreateClientSecretRequest(DateTime? ExpiresAt);

public sealed record CreateClientSecretCommand(Guid ClientId, CreateClientSecretRequest Request) : IRequest<ClientSecretResponse>;

