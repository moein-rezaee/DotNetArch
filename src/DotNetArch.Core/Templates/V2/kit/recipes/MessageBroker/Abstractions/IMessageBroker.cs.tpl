namespace {{Prefix}}.Kit.MessageBroker.Abstractions;

/// <summary>
/// Provider-neutral publish/subscribe messaging over named destinations (queues/topics, depending on the provider).
/// Messages are serialised as JSON. Provider failures surface as <see cref="MessageBrokerException"/>;
/// cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IMessageBroker
{
    /// <summary>Name of the provider selected by configuration for this deployment.</summary>
    string ProviderName { get; }

    Task PublishAsync<T>(string destination, T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts consuming <paramref name="destination"/>. A message is acknowledged when <paramref name="handler"/> completes and
    /// rejected (without requeue) when it throws. Dispose the returned subscription to stop consuming.
    /// </summary>
    Task<IAsyncDisposable> SubscribeAsync<T>(string destination, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
}
