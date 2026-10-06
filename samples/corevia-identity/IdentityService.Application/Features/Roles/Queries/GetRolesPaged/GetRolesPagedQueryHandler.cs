using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Roles.Queries.GetRolesPaged;

public sealed class GetRolesPagedQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetRolesPagedQuery, PagedResponse<RoleListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<PagedResponse<RoleListItemDto>> Handle(GetRolesPagedQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<Role>();
        var query = repo.Query().OrderBy(r => r.Name);

        var total = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RoleListItemDto(r.Id, r.Name, r.DisplayName, r.Description))
            .ToList();

        var response = new PagedResponse<RoleListItemDto>(items, request.PageNumber, request.PageSize, total);
        return Task.FromResult(response);
    }
}

