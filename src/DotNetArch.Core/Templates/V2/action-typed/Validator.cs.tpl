using FluentValidation;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

internal sealed class {{ActionName}}{{Entity}}{{RequestKind}}Validator : AbstractValidator<{{ActionName}}{{Entity}}{{RequestKind}}>
{
    public {{ActionName}}{{Entity}}{{RequestKind}}Validator()
    {
{{ValidatorRules}}
    }
}
