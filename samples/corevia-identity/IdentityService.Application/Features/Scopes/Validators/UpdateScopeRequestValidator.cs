using FluentValidation;
using IdentityService.Application.Features.Scopes.Commands.UpdateScope;

namespace IdentityService.Application.Features.Scopes.Validators;

public sealed class UpdateScopeRequestValidator : AbstractValidator<UpdateScopeRequest>
{
    public UpdateScopeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

