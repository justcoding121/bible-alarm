#nullable enable

using System;

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class FontBodyPointsDensityScalerTests
{
    [Fact]
    public void ScalePointsWithDensityClamp_falls_back_to_unit_density_when_density_not_positive()
    {
        Assert.Equal(18.0, FontBodyPointsDensityScaler.ScalePointsWithDensityClamp(18.0, density: 0, maxDensityMultiplier: 2.0));
    }

    [Fact]
    public void ScalePointsWithDensityClamp_caps_multiplier_using_density_ceiling_parameter()
    {
        Assert.Equal(20.0, FontBodyPointsDensityScaler.ScalePointsWithDensityClamp(10.0, density: 10.0, maxDensityMultiplier: 2.0));
    }

    [Fact]
    public void ScalePointsWithDensityClamp_throws_when_max_multiplier_not_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FontBodyPointsDensityScaler.ScalePointsWithDensityClamp(10.0, density: 2.0, maxDensityMultiplier: 0));
    }
}
