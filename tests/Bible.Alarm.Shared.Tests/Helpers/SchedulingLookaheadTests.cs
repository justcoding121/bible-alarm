#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class SchedulingLookaheadTests
{
    [Fact]
    public void FromDaysOrZero_returns_zero_when_days_are_non_positive()
    {
        Assert.Equal(TimeSpan.Zero, SchedulingLookahead.FromDaysOrZero(0));
        Assert.Equal(TimeSpan.Zero, SchedulingLookahead.FromDaysOrZero(-4));
    }

    [Fact]
    public void FromDaysOrZero_maps_positive_days_to_TimeSpan_FromDays()
    {
        Assert.Equal(TimeSpan.FromDays(9), SchedulingLookahead.FromDaysOrZero(9));
    }
}
