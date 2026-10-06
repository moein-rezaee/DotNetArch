using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Queries.GetClientSecrets;

public sealed class GetClientSecretsQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetClientSecretsQuery, IReadOnlyCollection<ClientSecretResponse>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<ClientSecretResponse>> Handle(GetClientSecretsQuery request, CancellationToken cancellationToken)
    {
        var secretRepo = _unitOfWork.Repository<ClientSecret>();

        var secrets = secretRepo.Query()
            .Where(s => s.ClientId == request.ClientId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ClientSecretResponse(s.Id, string.Empty, s.CreatedAt, s.ExpiresAt))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ClientSecretResponse>>(secrets);
    }
}

