#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class RepeatingFireOccurrencePlannerTests
{
    [Fact]
    public void CollectFutureOccurrences_collects_only_fires_strictly_inside_horizon_window()
    {
        var anchor = new DateTimeOffset(2035, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var results = RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
            c => c.AddHours(1),
            () => anchor,
            TimeSpan.FromHours(3),
            maxIterations: 20);

        Assert.Equal(
            new[] { anchor.AddHours(1), anchor.AddHours(2), anchor.AddHours(3) },
            results);
    }

    [Fact]
    public void CollectFutureOccurrences_skips_fires_not_after_latest_clock_reading_before_collecting()
    {
        var anchor = new DateTimeOffset(2038, 2, 3, 12, 0, 0, TimeSpan.Zero);
        var past = anchor.AddHours(-1);

        DateTimeOffset Next(DateTimeOffset cursor)
        {
            if (cursor == anchor)
            {
                return past;
            }

            if (cursor == past)
            {
                return anchor.AddHours(1);
            }

            return anchor.AddDays(30);
        }

        var results = RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
            Next,
            () => anchor,
            TimeSpan.FromHours(24),
            maxIterations: 20);

        Assert.Equal(anchor.AddHours(1), Assert.Single(results));
    }

    [Fact]
    public void CollectFutureOccurrences_throws_when_next_fire_delegate_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
                null!,
                () => DateTimeOffset.UtcNow,
                TimeSpan.FromDays(1),
                maxIterations: 1));
    }

    [Fact]
    public void CollectFutureOccurrences_throws_when_clock_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
                c => c.AddMinutes(1),
                null!,
                TimeSpan.FromDays(1),
                maxIterations: 1));
    }
}
