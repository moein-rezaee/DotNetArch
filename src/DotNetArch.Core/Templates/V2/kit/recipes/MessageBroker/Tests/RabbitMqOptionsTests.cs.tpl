using {{Prefix}}.Kit.MessageBroker.Providers.RabbitMq;
using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.MessageBroker.Tests;

public class RabbitMqOptionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> Valid() => new()
    {
        ["MessageBroker:RabbitMq:Host"] = "rabbit",
        [RabbitMqOptions.UserKey] = "app",
        [RabbitMqOptions.PasswordKey] = "pw"
    };

    [Fact]
    public void The_host_is_required_and_the_error_names_the_key()
    {
        var values = Valid();
        values.Remove("MessageBroker:RabbitMq:Host");

        Assert.Contains("MessageBroker:RabbitMq:Host", Assert.Throws<InvalidOperationException>(() => RabbitMqOptions.Bind(Config(values))).Message);
    }

    [Fact]
    public void Credentials_are_secrets_and_both_are_required()
    {
        var values = Valid();
        values.Remove(RabbitMqOptions.PasswordKey);

        var error = Assert.Throws<InvalidOperationException>(() => RabbitMqOptions.Bind(Config(values)));

        Assert.Contains(RabbitMqOptions.PasswordKey, error.Message);
    }

    [Fact]
    public void Defaults_apply_to_port_and_virtual_host()
    {
        var options = RabbitMqOptions.Bind(Config(Valid()));

        Assert.Equal(5672, options.Port);
        Assert.Equal("/", options.VirtualHost);
        Assert.Equal("app", options.UserName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("abc")]
    public void An_invalid_port_is_rejected(string port)
    {
        var values = Valid();
        values["MessageBroker:RabbitMq:Port"] = port;

        Assert.Throws<InvalidOperationException>(() => RabbitMqOptions.Bind(Config(values)));
    }
}
