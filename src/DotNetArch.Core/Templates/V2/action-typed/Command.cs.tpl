{{DtoUsing}}using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

/// <summary>{{Summary}}</summary>
public sealed record {{ActionName}}{{Entity}}{{RequestKind}}({{RecordParameters}}) : IRequest{{ResultGeneric}};
