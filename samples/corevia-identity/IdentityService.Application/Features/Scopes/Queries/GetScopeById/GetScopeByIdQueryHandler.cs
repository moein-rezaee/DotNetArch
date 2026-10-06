using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Queries.GetScopeById;

public sealed class GetScopeByIdQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetScopeByIdQuery, ScopeDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<ScopeDetailDto> Handle(GetScopeByIdQuery request, CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var mappingRepo = _unitOfWork.Repository<ScopePermission>();

        var scope = scopeRepo.Query().FirstOrDefault(s => s.Id == request.Id);
        if (scope is null)
        {
            throw new NotFoundException("Scope not found.");
        }

        var permissionIds = mappingRepo.Query()
            .Where(sp => sp.ScopeId == scope.Id)
            .Select(sp => sp.PermissionId)
            .ToList();

        var permissions = permissionRepo.Query()
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => new PermissionDto(p.Id, p.Key, p.DisplayName, p.Description))
            .ToList();

        var dto = new ScopeDetailDto(scope.Id, scope.Name, scope.DisplayName, scope.Description, permissions);
        return Task.FromResult(dto);
    }
}

