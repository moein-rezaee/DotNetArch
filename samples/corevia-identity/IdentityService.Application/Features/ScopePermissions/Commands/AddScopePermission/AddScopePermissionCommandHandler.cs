using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.ScopePermissions.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.ScopePermissions.Commands.AddScopePermission;

public sealed class AddScopePermissionCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<AddScopePermissionCommand, IReadOnlyCollection<ScopePermissionDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IReadOnlyCollection<ScopePermissionDto>> Handle(AddScopePermissionCommand request, CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var mappingRepo = _unitOfWork.Repository<ScopePermission>();

        var scope = await scopeRepo.GetByIdAsync(request.ScopeId, cancellationToken)
                    ?? throw new NotFoundException("Scope not found.");

        var permission = await permissionRepo.GetByIdAsync(request.Request.PermissionId, cancellationToken)
                        ?? throw new NotFoundException("Permission not found.");

        var existing = mappingRepo.Query()
            .FirstOrDefault(sp => sp.ScopeId == scope.Id && sp.PermissionId == permission.Id);

        if (existing is null)
        {
            await mappingRepo.AddAsync(new ScopePermission
            {
                ScopeId = scope.Id,
                PermissionId = permission.Id
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mappings = mappingRepo.Query()
            .Where(sp => sp.ScopeId == scope.Id)
            .ToList();

        var permissionIds = mappings.Select(m => m.PermissionId).ToList();
        var permissions = permissionRepo.Query()
            .Where(p => permissionIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => new PermissionDto(p.Id, p.Key, p.DisplayName, p.Description));

        var result = mappings
            .Select(m =>
            {
                permissions.TryGetValue(m.PermissionId, out var permissionDto);
                permissionDto ??= new PermissionDto(m.PermissionId, string.Empty, string.Empty, null);
                return new ScopePermissionDto(permissionDto);
            })
            .ToArray();

        return result;
    }
}
