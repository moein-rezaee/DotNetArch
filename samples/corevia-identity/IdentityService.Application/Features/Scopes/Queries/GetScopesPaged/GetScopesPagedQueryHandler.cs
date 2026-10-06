using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Queries.GetScopesPaged;

public sealed class GetScopesPagedQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetScopesPagedQuery, PagedResponse<ScopeListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<PagedResponse<ScopeListItemDto>> Handle(GetScopesPagedQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<Scope>();
        var query = repo.Query().OrderBy(s => s.Name);

        var total = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new ScopeListItemDto(s.Id, s.Name, s.DisplayName, s.Description))
            .ToList();

        var response = new PagedResponse<ScopeListItemDto>(items, request.PageNumber, request.PageSize, total);
        return Task.FromResult(response);
    }
}

