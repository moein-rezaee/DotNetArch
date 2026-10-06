using System;
using System.Collections.Generic;
using IdentityService.Application.Features.Clients.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Clients.Queries.GetClientSecrets;

public sealed record GetClientSecretsQuery(Guid ClientId) : IRequest<IReadOnlyCollection<ClientSecretResponse>>;

