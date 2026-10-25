{{DtoUsing}}using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

internal sealed class {{ActionName}}{{Entity}}{{RequestKind}}Handler : IRequestHandler<{{ActionName}}{{Entity}}{{RequestKind}}{{ResultGenericComma}}>
{
    public Task{{TaskGeneric}} Handle({{ActionName}}{{Entity}}{{RequestKind}} request, CancellationToken cancellationToken)
    {
        // TODO(business logic): implement '{{ActionName}}'. Rules live in the domain; this handler orchestrates ports (unit of work, kits).
        throw new NotImplementedException("{{ActionName}}{{Entity}} is not implemented yet.");
    }
}
