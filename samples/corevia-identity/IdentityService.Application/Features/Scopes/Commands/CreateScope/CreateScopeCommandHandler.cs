using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.Scopes.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Commands.CreateScope;

public sealed class CreateScopeCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateScopeCommand, ScopeDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ScopeDetailDto> Handle(CreateScopeCommand request, CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var normalizedName = request.Request.Name.Trim();
        if (scopeRepo.Query().Any(s => s.Name == normalizedName))
        {
            throw new BadRequestException("Scope name already exists.", "duplicate_scope_name");
        }

        var scope = new Scope
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            DisplayName = request.Request.DisplayName.Trim(),
            Description = request.Request.Description,
            CreatedAt = DateTime.UtcNow
        };

        await scopeRepo.AddAsync(scope, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ScopePermissions از طریق اندپوینت‌های جداگانه مدیریت می‌شوند
        var permissions = new List<PermissionDto>();

        return new ScopeDetailDto(scope.Id, scope.Name, scope.DisplayName, scope.Description, permissions);
    }
}
