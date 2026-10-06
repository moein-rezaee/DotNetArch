using FluentValidation;
using IdentityService.Application.Features.Tenants.Commands.UpdateTenant;

namespace IdentityService.Application.Features.Tenants.Validators;

public sealed class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

