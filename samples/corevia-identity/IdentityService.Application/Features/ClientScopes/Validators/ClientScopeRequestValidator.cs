using FluentValidation;
using IdentityService.Application.Features.ClientScopes.Dtos;

namespace IdentityService.Application.Features.ClientScopes.Validators;

public sealed class ClientScopeRequestValidator : AbstractValidator<ClientScopeRequest>
{
    public ClientScopeRequestValidator()
    {
        RuleFor(x => x.ScopeId)
            .NotEmpty();
    }
}

