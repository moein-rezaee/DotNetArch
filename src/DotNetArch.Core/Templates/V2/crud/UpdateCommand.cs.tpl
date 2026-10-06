using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};

public sealed record Update{{Entity}}Command(Guid Id, string Name) : IRequest<{{Entity}}Dto>;
