namespace {{App}}.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}

public abstract record DomainEvent(DateTime OccurredAtUtc) : IDomainEvent;
