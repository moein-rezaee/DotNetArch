using FluentValidation;

namespace {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};

internal sealed class Get{{Plural}}QueryValidator : AbstractValidator<Get{{Plural}}Query>
{
    public Get{{Plural}}QueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
