using {{App}}.Domain.Common;
using MediatR;

namespace {{App}}.Application.Common.Notifications;

/// <summary>Wraps a domain event so Application handlers can subscribe with <c>INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;</c> without Domain knowing MediatR.</summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : IDomainEvent;
