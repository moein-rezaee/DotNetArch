using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

public sealed record {{ActionName}}{{Entity}}Query(Guid Id) : IRequest<{{Entity}}Dto>;
