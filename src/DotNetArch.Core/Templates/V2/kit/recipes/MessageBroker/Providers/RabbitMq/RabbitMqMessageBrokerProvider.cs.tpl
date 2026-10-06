using System.Text.Json;
using {{Prefix}}.Kit.MessageBroker.Abstractions;
using {{Prefix}}.Kit.MessageBroker.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace {{Prefix}}.Kit.MessageBroker.Providers.RabbitMq;

/// <summary>RabbitMQ provider (RabbitMQ.Client 7). Destinations are durable queues; the connection is opened on first use.</summary>
public sealed class RabbitMqMessageBrokerProvider : IMessageBrokerProvider
{
    public const string ProviderName = "RabbitMq";

    public string Name => ProviderName;

    public IMessageBroker Create(IConfiguration configuration) => new RabbitMqMessageBroker(RabbitMqOptions.Bind(configuration));
}

internal sealed class RabbitMqMessageBroker(RabbitMqOptions options) : IMessageBroker, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public string ProviderName => RabbitMqMessageBrokerProvider.ProviderName;

    public async Task PublishAsync<T>(string destination, T message, CancellationToken cancellationToken = default)
    {
        RequireDestination(destination);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await DeclareAsync(channel, destination, cancellationToken).ConfigureAwait(false);

            var properties = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent };
            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            await channel.BasicPublishAsync(string.Empty, destination, mandatory: false, properties, body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
        {
            throw new MessageBrokerException($"RabbitMQ publish to '{destination}' failed: {ex.Message}", "message_broker_unavailable", ex);
        }
    }

    public async Task<IAsyncDisposable> SubscribeAsync<T>(string destination, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        RequireDestination(destination);
        ArgumentNullException.ThrowIfNull(handler);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await DeclareAsync(channel, destination, cancellationToken).ConfigureAwait(false);
            await channel.BasicQosAsync(0, 10, false, cancellationToken).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                try
                {
                    var message = JsonSerializer.Deserialize<T>(delivery.Body.Span)!;
                    await handler(message, CancellationToken.None).ConfigureAwait(false);
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false).ConfigureAwait(false);
                }
            };

            await channel.BasicConsumeAsync(destination, autoAck: false, consumer, cancellationToken).ConfigureAwait(false);
            return channel;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException)
        {
            throw new MessageBrokerException($"RabbitMQ subscribe to '{destination}' failed: {ex.Message}", "message_broker_unavailable", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private static Task DeclareAsync(IChannel channel, string destination, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.UserName,
                Password = options.Password
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void RequireDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination must not be empty.", nameof(destination));
    }
}

public static class RabbitMqMessageBrokerServiceCollectionExtensions
{
    /// <summary>Registers the RabbitMQ provider so <c>AddMessageBrokerKit</c> can select it.</summary>
    public static IServiceCollection AddRabbitMqMessageBrokerProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageBrokerProvider, RabbitMqMessageBrokerProvider>());
        return services;
    }
}
