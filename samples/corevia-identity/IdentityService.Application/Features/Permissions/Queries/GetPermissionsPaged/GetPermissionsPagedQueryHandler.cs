using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Queries.GetPermissionsPaged;

public sealed class GetPermissionsPagedQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetPermissionsPagedQuery, PagedResponse<PermissionDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<PagedResponse<PermissionDto>> Handle(GetPermissionsPagedQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<Permission>();
        var query = repo.Query().OrderBy(p => p.Key);

        var total = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PermissionDto(p.Id, p.Key, p.DisplayName, p.Description))
            .ToList();

        var response = new PagedResponse<PermissionDto>(items, request.PageNumber, request.PageSize, total);
        return Task.FromResult(response);
    }
}

