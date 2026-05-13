#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Detects whether raw display metrics are usable for computing scaling factors.
/// </summary>
public static class MeasurableScreenDimensionsGate
{
    public static bool HasPositiveExtents(double width, double height, double density)
    {
        return width > 0 && height > 0 && density > 0;
    }
}
