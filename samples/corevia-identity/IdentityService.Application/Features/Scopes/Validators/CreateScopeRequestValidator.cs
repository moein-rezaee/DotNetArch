using FluentValidation;
using IdentityService.Application.Features.Scopes.Commands.CreateScope;

namespace IdentityService.Application.Features.Scopes.Validators;

public sealed class CreateScopeRequestValidator : AbstractValidator<CreateScopeRequest>
{
    public CreateScopeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

