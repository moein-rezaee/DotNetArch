using {{App}}.Application.Common.Notifications;
using {{App}}.Domain.Events.{{EventPlural}};
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Events;

/// <summary>Reacts to <see cref="{{EventEntity}}{{EventName}}Event"/>. TODO: implement the reaction (call a command, update a read model, notify).</summary>
internal sealed class On{{EventEntity}}{{EventName}}Handler : INotificationHandler<DomainEventNotification<{{EventEntity}}{{EventName}}Event>>
{
    public Task Handle(DomainEventNotification<{{EventEntity}}{{EventName}}Event> notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
