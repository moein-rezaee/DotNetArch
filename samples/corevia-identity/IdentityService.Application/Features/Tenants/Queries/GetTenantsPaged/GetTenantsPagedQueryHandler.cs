using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Queries.GetTenantsPaged;

public sealed class GetTenantsPagedQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetTenantsPagedQuery, PagedResponse<TenantListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<PagedResponse<TenantListItemDto>> Handle(GetTenantsPagedQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<Tenant>();
        var query = repo.Query().OrderBy(t => t.Name);

        var total = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TenantListItemDto(t.Id, t.Name, t.DisplayName, t.IsActive))
            .ToList();

        var response = new PagedResponse<TenantListItemDto>(items, request.PageNumber, request.PageSize, total);
        return Task.FromResult(response);
    }
}

