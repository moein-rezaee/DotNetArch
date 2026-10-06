namespace {{App}}.Domain.Common;

/// <summary>Base type for aggregates/entities: identity, audit timestamps and raised domain events.</summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime? UpdatedAtUtc { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void MarkCreated(DateTime nowUtc) => CreatedAtUtc = nowUtc;

    protected void MarkUpdated(DateTime nowUtc) => UpdatedAtUtc = nowUtc;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
