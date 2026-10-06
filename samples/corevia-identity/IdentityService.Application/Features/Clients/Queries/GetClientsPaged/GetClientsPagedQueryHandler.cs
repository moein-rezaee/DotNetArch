using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Clients.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Clients.Queries.GetClientsPaged;

public sealed class GetClientsPagedQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetClientsPagedQuery, PagedResponse<ClientListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<PagedResponse<ClientListItemDto>> Handle(GetClientsPagedQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<Client>();
        var query = repo.Query().OrderBy(c => c.Name);

        var total = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClientListItemDto(c.Id, c.ClientId, c.Name, c.Description, c.IsActive))
            .ToList();

        var response = new PagedResponse<ClientListItemDto>(items, request.PageNumber, request.PageSize, total);
        return Task.FromResult(response);
    }
}

