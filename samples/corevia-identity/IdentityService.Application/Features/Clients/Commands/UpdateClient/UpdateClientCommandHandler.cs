using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Commands.UpdateClient;

public sealed class UpdateClientCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateClientCommand, ClientDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ClientDetailDto> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var clientRepo = _unitOfWork.Repository<Client>();
        var scopeRepo = _unitOfWork.Repository<Scope>();

        var client = await clientRepo.GetByIdAsync(request.Id, cancellationToken)
                     ?? throw new NotFoundException("Client not found.");

        var normalizedName = request.Request.Name.Trim();

        client.Name = normalizedName;
        client.Description = request.Request.Description;
        client.IsActive = request.Request.IsActive;
        client.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ClientScopes از طریق اندپوینت‌های جداگانه مدیریت می‌شوند
        var scopes = new List<ScopeListItemDto>();

        return new ClientDetailDto(client.Id, client.ClientId, client.Name, client.Description, client.IsActive, scopes);
    }
}
