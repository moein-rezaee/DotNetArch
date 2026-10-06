using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.ClientScopes.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.ClientScopes.Commands.AddClientScope;

public sealed class AddClientScopeCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<AddClientScopeCommand, IReadOnlyCollection<ClientScopeDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IReadOnlyCollection<ClientScopeDto>> Handle(AddClientScopeCommand request, CancellationToken cancellationToken)
    {
        var clientRepo = _unitOfWork.Repository<Client>();
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var mappingRepo = _unitOfWork.Repository<ClientScope>();

        var client = await clientRepo.GetByIdAsync(request.ClientId, cancellationToken)
                     ?? throw new NotFoundException("Client not found.");

        var scope = await scopeRepo.GetByIdAsync(request.Request.ScopeId, cancellationToken)
                    ?? throw new NotFoundException("Scope not found.");

        var existing = mappingRepo.Query()
            .FirstOrDefault(cs => cs.ClientId == client.Id && cs.ScopeId == scope.Id);

        if (existing is null)
        {
            await mappingRepo.AddAsync(new ClientScope
            {
                ClientId = client.Id,
                ScopeId = scope.Id
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mappings = mappingRepo.Query()
            .Where(cs => cs.ClientId == client.Id)
            .ToList();

        var scopeIds = mappings.Select(m => m.ScopeId).ToList();
        var scopes = scopeRepo.Query()
            .Where(s => scopeIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => new ScopeListItemDto(s.Id, s.Name, s.DisplayName, s.Description));

        var result = mappings
            .Select(m =>
            {
                scopes.TryGetValue(m.ScopeId, out var scopeDto);
                scopeDto ??= new ScopeListItemDto(m.ScopeId, string.Empty, string.Empty, null);
                return new ClientScopeDto(scopeDto);
            })
            .ToArray();

        return result;
    }
}
