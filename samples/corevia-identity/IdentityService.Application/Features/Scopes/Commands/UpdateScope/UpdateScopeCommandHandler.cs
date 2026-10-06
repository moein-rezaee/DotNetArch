using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Commands.UpdateScope;

public sealed class UpdateScopeCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateScopeCommand, ScopeDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ScopeDetailDto> Handle(UpdateScopeCommand request, CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var scope = await scopeRepo.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("Scope not found.");

        var normalizedName = request.Request.Name.Trim();
        if (!string.Equals(scope.Name, normalizedName, StringComparison.Ordinal) &&
            scopeRepo.Query().Any(s => s.Name == normalizedName))
        {
            throw new BadRequestException("Scope name already exists.", "duplicate_scope_name");
        }

        scope.Name = normalizedName;
        scope.DisplayName = request.Request.DisplayName.Trim();
        scope.Description = request.Request.Description;
        scope.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ScopePermissions از طریق اندپوینت‌های جداگانه مدیریت می‌شوند
        var permissions = new List<PermissionDto>();

        return new ScopeDetailDto(scope.Id, scope.Name, scope.DisplayName, scope.Description, permissions);
    }
}
