using FluentValidation;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

internal sealed class {{ActionName}}{{Entity}}CommandValidator : AbstractValidator<{{ActionName}}{{Entity}}Command>
{
    public {{ActionName}}{{Entity}}CommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
