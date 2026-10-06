using FluentValidation;
using IdentityService.Application.Features.ScopePermissions.Dtos;

namespace IdentityService.Application.Features.ScopePermissions.Validators;

public sealed class ScopePermissionRequestValidator : AbstractValidator<ScopePermissionRequest>
{
    public ScopePermissionRequestValidator()
    {
        RuleFor(x => x.PermissionId)
            .NotEmpty();
    }
}

