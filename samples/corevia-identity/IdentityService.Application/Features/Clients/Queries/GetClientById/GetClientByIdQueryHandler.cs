using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Queries.GetClientById;

public sealed class GetClientByIdQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetClientByIdQuery, ClientDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<ClientDetailDto> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        var clientRepo = _unitOfWork.Repository<Client>();
        var clientScopeRepo = _unitOfWork.Repository<ClientScope>();
        var scopeRepo = _unitOfWork.Repository<Scope>();

        var client = clientRepo.Query().FirstOrDefault(c => c.Id == request.Id);
        if (client is null)
        {
            throw new NotFoundException("Client not found.");
        }

        var scopeIds = clientScopeRepo.Query()
            .Where(cs => cs.ClientId == client.Id)
            .Select(cs => cs.ScopeId)
            .ToList();

        var scopes = scopeRepo.Query()
            .Where(s => scopeIds.Contains(s.Id))
            .Select(s => new ScopeListItemDto(s.Id, s.Name, s.DisplayName, s.Description))
            .ToList();

        var dto = new ClientDetailDto(client.Id, client.ClientId, client.Name, client.Description, client.IsActive, scopes);
        return Task.FromResult(dto);
    }
}

