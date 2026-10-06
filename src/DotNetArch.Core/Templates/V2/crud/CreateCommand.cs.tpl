using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};

public sealed record Create{{Entity}}Command(string Name) : IRequest<{{Entity}}Dto>;
