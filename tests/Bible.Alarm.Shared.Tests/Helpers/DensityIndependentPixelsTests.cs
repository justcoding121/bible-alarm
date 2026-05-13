#nullable enable

using System;

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class DensityIndependentPixelsTests
{
    [Fact]
    public void WidthPixelsToDp_divides_width_by_density()
    {
        Assert.Equal(400.0, DensityIndependentPixels.WidthPixelsToDp(800.0, density: 2.0));
    }

    [Fact]
    public void WidthPixelsToDp_throws_when_density_not_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DensityIndependentPixels.WidthPixelsToDp(widthPixels: 100.0, density: 0));
    }
}
