#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PlannerIterationClampTests
{
    [Fact]
    public void Normalize_returns_minimum_when_configured_below_range()
    {
        Assert.Equal(5, PlannerIterationClamp.Normalize(1, minimumInclusive: 5, maximumInclusive: 10));
    }

    [Fact]
    public void Normalize_returns_maximum_when_configured_above_range()
    {
        Assert.Equal(10, PlannerIterationClamp.Normalize(99, minimumInclusive: 5, maximumInclusive: 10));
    }

    [Fact]
    public void Normalize_returns_configured_when_within_range()
    {
        Assert.Equal(7, PlannerIterationClamp.Normalize(7, minimumInclusive: 5, maximumInclusive: 10));
    }
}
