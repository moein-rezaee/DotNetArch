using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Queries.GetPermissionById;

public sealed class GetPermissionByIdQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetPermissionByIdQuery, PermissionDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PermissionDto> Handle(GetPermissionByIdQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<Permission>();
        var permission = await repo.GetByIdAsync(request.Id, cancellationToken)
                         ?? throw new NotFoundException("Permission not found.");

        return new PermissionDto(permission.Id, permission.Key, permission.DisplayName, permission.Description);
    }
}

