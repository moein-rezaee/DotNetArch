using {{App}}.Domain.Common;

namespace {{App}}.Domain.Events.{{Plural}};

/// <summary>Raised by <see cref="Entities.{{Entity}}"/> when '{{EventName}}' happens. Add the data subscribers need as parameters.</summary>
public sealed record {{Entity}}{{EventName}}Event(Guid {{Entity}}Id, DateTime OccurredAtUtc) : DomainEvent(OccurredAtUtc);
