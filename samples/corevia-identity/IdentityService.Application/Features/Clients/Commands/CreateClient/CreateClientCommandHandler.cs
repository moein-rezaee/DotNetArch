using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.CreateClient;

public sealed class CreateClientCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateClientCommand, ClientDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ClientDetailDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var clientRepo = _unitOfWork.Repository<Client>();
        var scopeRepo = _unitOfWork.Repository<Scope>();

        var normalizedName = request.Request.Name.Trim();

        string clientId;
        do
        {
            clientId = Guid.NewGuid().ToString("N");
        } while (clientRepo.Query().Any(c => c.ClientId == clientId));

        var client = new Client
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = normalizedName,
            Description = request.Request.Description,
            IsActive = request.Request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await clientRepo.AddAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ClientScopes از طریق اندپوینت‌های جداگانه مدیریت می‌شوند
        var scopes = new List<ScopeListItemDto>();

        return new ClientDetailDto(client.Id, client.ClientId, client.Name, client.Description, client.IsActive, scopes);
    }
}
