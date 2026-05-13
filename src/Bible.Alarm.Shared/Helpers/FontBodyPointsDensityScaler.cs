#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Scales typography points using logical density while clamping extreme density spikes.
/// </summary>
public static class FontBodyPointsDensityScaler
{
    public static double ScalePointsWithDensityClamp(double basePoints, double density, double maxDensityMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDensityMultiplier, 0);

        var effectiveDensity = density > 0 ? density : 1.0;
        return basePoints * Math.Min(effectiveDensity, maxDensityMultiplier);
    }
}
