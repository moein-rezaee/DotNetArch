using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Users.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Users.Queries.GetUsersPaged;

public sealed class GetUsersPagedQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetUsersPagedQuery, PagedResponse<UserListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<PagedResponse<UserListItemDto>> Handle(GetUsersPagedQuery request, CancellationToken cancellationToken)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var userRoleRepo = _unitOfWork.Repository<UserRole>();

        var query = userRepo.Query().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phone = request.Phone.Trim();
            query = query.Where(u => u.PhoneNumber.Contains(phone));
        }

        if (request.TenantId.HasValue)
        {
            var tenantId = request.TenantId.Value;
            var userIds = userTenantRepo.Query()
                .Where(ut => ut.TenantId == tenantId)
                .Select(ut => ut.UserId)
                .ToList();

            query = query.Where(u => userIds.Contains(u.Id));
        }

        if (request.RoleId.HasValue)
        {
            var roleId = request.RoleId.Value;
            var userIds = userRoleRepo.Query()
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => ur.UserId)
                .ToList();

            query = query.Where(u => userIds.Contains(u.Id));
        }

        query = query.OrderBy(u => u.CreatedAt);

        var total = query.Count();
        var items = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserListItemDto(u.Id, u.PhoneNumber, u.CreatedAt, u.UpdatedAt, u.IsActive))
            .ToList();

        var response = new PagedResponse<UserListItemDto>(items, request.PageNumber, request.PageSize, total);
        return Task.FromResult(response);
    }
}
