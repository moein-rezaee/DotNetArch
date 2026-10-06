using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.RevokeClientSecret;

public sealed class RevokeClientSecretCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RevokeClientSecretCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RevokeClientSecretCommand request, CancellationToken cancellationToken)
    {
        var secretRepo = _unitOfWork.Repository<ClientSecret>();

        var secret = await secretRepo.GetByIdAsync(request.SecretId, cancellationToken);
        if (secret is null || secret.ClientId != request.ClientId)
        {
            throw new NotFoundException("Client secret not found.");
        }

        if (secret.RevokedAt is null)
        {
            secret.RevokedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

