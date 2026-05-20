#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class SchedulingLookaheadBibleAlarmTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void FromDaysOrZero_returns_zero_for_non_positive_days(int days)
    {
        Assert.Equal(TimeSpan.Zero, SchedulingLookahead.FromDaysOrZero(days));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    public void FromDaysOrZero_returns_day_span_for_positive_days(int days)
    {
        Assert.Equal(TimeSpan.FromDays(days), SchedulingLookahead.FromDaysOrZero(days));
    }
}
