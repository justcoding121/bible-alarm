#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MeasurableScreenDimensionsGateTests
{
    [Fact]
    public void HasPositiveExtents_returns_false_when_any_extent_non_positive()
    {
        Assert.False(MeasurableScreenDimensionsGate.HasPositiveExtents(width: 10, height: 10, density: 0));
    }

    [Fact]
    public void HasPositiveExtents_returns_true_when_width_height_and_density_are_positive()
    {
        Assert.True(MeasurableScreenDimensionsGate.HasPositiveExtents(width: 400, height: 800, density: 2));
    }
}
