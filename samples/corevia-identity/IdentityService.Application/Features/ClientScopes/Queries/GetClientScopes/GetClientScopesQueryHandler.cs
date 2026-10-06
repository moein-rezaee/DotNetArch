using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.ClientScopes.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.ClientScopes.Queries.GetClientScopes;

public sealed class GetClientScopesQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetClientScopesQuery, IReadOnlyCollection<ClientScopeDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<ClientScopeDto>> Handle(GetClientScopesQuery request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<ClientScope>();
        var scopeRepo = _unitOfWork.Repository<Scope>();

        var mappings = mappingRepo.Query()
            .Where(cs => cs.ClientId == request.ClientId)
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

        return Task.FromResult<IReadOnlyCollection<ClientScopeDto>>(result);
    }
}
