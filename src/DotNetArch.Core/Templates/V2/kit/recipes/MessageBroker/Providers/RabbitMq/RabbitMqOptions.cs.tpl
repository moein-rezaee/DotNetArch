using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.MessageBroker.Providers.RabbitMq;

/// <summary>
/// Validated RabbitMQ settings. Non-sensitive keys come from <c>MessageBroker:RabbitMq:*</c>; credentials are secrets read from
/// <c>RABBITMQ_USER</c> / <c>RABBITMQ_PASSWORD</c>. No host default and no silent fallback.
/// </summary>
internal sealed class RabbitMqOptions
{
    public const string SectionPath = "MessageBroker:RabbitMq";
    public const string UserKey = "RABBITMQ_USER";
    public const string PasswordKey = "RABBITMQ_PASSWORD";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public static RabbitMqOptions Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionPath);

        var host = section["Host"]?.Trim();
        if (string.IsNullOrEmpty(host))
            throw new InvalidOperationException($"Message broker option '{SectionPath}:Host' is required but is not configured.");

        var port = 5672;
        var rawPort = section["Port"]?.Trim();
        if (!string.IsNullOrEmpty(rawPort) && (!int.TryParse(rawPort, out port) || port is < 1 or > 65535))
            throw new InvalidOperationException($"Message broker option '{SectionPath}:Port' must be a port number but was '{rawPort}'.");

        var user = configuration[UserKey];
        var password = configuration[PasswordKey];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Message broker credentials are missing: set both '{UserKey}' and '{PasswordKey}'.");

        return new RabbitMqOptions
        {
            Host = host,
            Port = port,
            VirtualHost = section["VirtualHost"] is { Length: > 0 } virtualHost ? virtualHost : "/",
            UserName = user,
            Password = password
        };
    }
}
