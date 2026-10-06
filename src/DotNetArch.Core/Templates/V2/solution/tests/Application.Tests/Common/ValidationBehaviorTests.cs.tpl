using {{App}}.Application.Common.Behaviors;
using FluentValidation;

namespace {{App}}.Application.Tests.Common;

public class ValidationBehaviorTests
{
    private sealed record Ping(string Text);

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(ping => ping.Text).NotEmpty();
    }

    [Fact]
    public async Task Runs_the_handler_when_the_request_is_valid()
    {
        var behavior = new ValidationBehavior<Ping, string>(new[] { new PingValidator() });

        var result = await behavior.Handle(new Ping("hi"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Throws_before_the_handler_when_the_request_is_invalid()
    {
        var behavior = new ValidationBehavior<Ping, string>(new[] { new PingValidator() });
        var handlerRan = false;

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new Ping(string.Empty),
            _ =>
            {
                handlerRan = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None));

        Assert.False(handlerRan);
    }

    [Fact]
    public async Task Passes_through_when_no_validator_is_registered()
    {
        var behavior = new ValidationBehavior<Ping, string>(Array.Empty<IValidator<Ping>>());

        Assert.Equal("ok", await behavior.Handle(new Ping(string.Empty), _ => Task.FromResult("ok"), CancellationToken.None));
    }
}
