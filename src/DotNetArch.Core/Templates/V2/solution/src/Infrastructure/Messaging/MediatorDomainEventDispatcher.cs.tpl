using {{App}}.Application.Abstractions.Messaging;
using {{App}}.Application.Common.Notifications;
using {{App}}.Domain.Common;
using MediatR;

namespace {{App}}.Infrastructure.Messaging;

/// <summary>Publishes domain events in-process through MediatR as <see cref="DomainEventNotification{TEvent}"/>.</summary>
internal sealed class MediatorDomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent)!;
            await publisher.Publish(notification, cancellationToken);
        }
    }
}
