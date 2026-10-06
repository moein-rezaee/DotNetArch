using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.CreateClientSecret;

public sealed class CreateClientSecretCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateClientSecretCommand, ClientSecretResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ClientSecretResponse> Handle(CreateClientSecretCommand request, CancellationToken cancellationToken)
    {
        var clientRepo = _unitOfWork.Repository<Client>();
        var secretRepo = _unitOfWork.Repository<ClientSecret>();

        var client = await clientRepo.GetByIdAsync(request.ClientId, cancellationToken)
                     ?? throw new NotFoundException("Client not found.");

        var secretValue = GenerateSecret();
        var hash = Hash(secretValue);

        var secret = new ClientSecret
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Hash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.Request.ExpiresAt,
            RevokedAt = null
        };

        await secretRepo.AddAsync(secret, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ClientSecretResponse(secret.Id, secretValue, secret.CreatedAt, secret.ExpiresAt);
    }

    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}

