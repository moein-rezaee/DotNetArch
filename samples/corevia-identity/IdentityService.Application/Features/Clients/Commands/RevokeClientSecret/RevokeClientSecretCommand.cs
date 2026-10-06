using System;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.RevokeClientSecret;

public sealed record RevokeClientSecretCommand(Guid ClientId, Guid SecretId) : IRequest;

