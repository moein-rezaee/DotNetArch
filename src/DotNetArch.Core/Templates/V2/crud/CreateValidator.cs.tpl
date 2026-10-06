using {{App}}.Domain.Entities;
using FluentValidation;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};

internal sealed class Create{{Entity}}CommandValidator : AbstractValidator<Create{{Entity}}Command>
{
    public Create{{Entity}}CommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength({{Entity}}.NameMaxLength);
    }
}
