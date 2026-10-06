namespace {{App}}.Application.Tests.Support;

/// <summary>A clock that always returns the same instant, so timestamps are assertable.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static FixedTimeProvider At(int year, int month, int day) =>
        new(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;
}
