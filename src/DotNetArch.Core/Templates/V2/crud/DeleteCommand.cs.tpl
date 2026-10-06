using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};

public sealed record Delete{{Entity}}Command(Guid Id) : IRequest;
