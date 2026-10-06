using FluentValidation;
using IdentityService.Application.Features.Clients.Commands.UpdateClient;

namespace IdentityService.Application.Features.Clients.Validators;

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
