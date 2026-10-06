using FluentValidation;
using IdentityService.Application.Features.Clients.Commands.CreateClient;

namespace IdentityService.Application.Features.Clients.Validators;

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
