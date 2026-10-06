using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Application.Features.UserTenants.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.UserTenants.Commands.AddUserTenant;

public sealed class AddUserTenantCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<AddUserTenantCommand, IReadOnlyCollection<UserTenantDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IReadOnlyCollection<UserTenantDto>> Handle(AddUserTenantCommand request, CancellationToken cancellationToken)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var tenantRepo = _unitOfWork.Repository<Tenant>();
        var mappingRepo = _unitOfWork.Repository<UserTenant>();

        var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        var tenant = await tenantRepo.GetByIdAsync(request.Request.TenantId, cancellationToken)
                     ?? throw new NotFoundException("Tenant not found.");

        var existing = mappingRepo.Query()
            .Where(ut => ut.UserId == user.Id && ut.TenantId == tenant.Id)
            .FirstOrDefault();

        if (existing is null)
        {
            await mappingRepo.AddAsync(new UserTenant
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                IsDefault = request.Request.IsDefault
            }, cancellationToken);
        }
        else
        {
            existing.IsDefault = request.Request.IsDefault || existing.IsDefault;
        }

        if (request.Request.IsDefault)
        {
            var allMappings = mappingRepo.Query()
                .Where(ut => ut.UserId == user.Id)
                .ToList();

            foreach (var m in allMappings.Where(m => m.TenantId != tenant.Id))
            {
                m.IsDefault = false;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mappings = mappingRepo.Query()
            .Where(ut => ut.UserId == user.Id)
            .ToList();

        var tenantIds = mappings.Select(m => m.TenantId).ToList();
        var tenantDtos = tenantRepo.Query()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id, t => new TenantListItemDto(t.Id, t.Name, t.DisplayName, t.IsActive));

        var result = mappings
            .Select(m =>
            {
                tenantDtos.TryGetValue(m.TenantId, out var tenantDto);
                tenantDto ??= new TenantListItemDto(m.TenantId, string.Empty, string.Empty, false);
                return new UserTenantDto(tenantDto, m.IsDefault);
            })
            .ToArray();

        return result;
    }
}
