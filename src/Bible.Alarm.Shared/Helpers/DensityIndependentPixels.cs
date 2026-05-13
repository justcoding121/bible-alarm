#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Converts pixel measurements reported by the OS into density-independent pixels (dp).
/// </summary>
public static class DensityIndependentPixels
{
    public static double WidthPixelsToDp(double widthPixels, double density)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(density, 0);
        return widthPixels / density;
    }
}
