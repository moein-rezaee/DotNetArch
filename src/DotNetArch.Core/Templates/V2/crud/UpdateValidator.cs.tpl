using {{App}}.Domain.Entities;
using FluentValidation;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};

internal sealed class Update{{Entity}}CommandValidator : AbstractValidator<Update{{Entity}}Command>
{
    public Update{{Entity}}CommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength({{Entity}}.NameMaxLength);
    }
}
