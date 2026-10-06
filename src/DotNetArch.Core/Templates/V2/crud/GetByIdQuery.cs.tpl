using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;

public sealed record Get{{Entity}}ByIdQuery(Guid Id) : IRequest<{{Entity}}Dto>;
