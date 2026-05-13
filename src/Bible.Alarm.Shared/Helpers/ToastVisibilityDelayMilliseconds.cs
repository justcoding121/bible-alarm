#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Converts fractional-second toast durations to integer milliseconds with overflow and non-finite guards.
/// </summary>
public static class ToastVisibilityDelayMilliseconds
{
    public static int FromNonNegativeUiMilliseconds(int milliseconds) =>
        milliseconds < 0 ? 0 : milliseconds;

    public static int FromToastDurationSeconds(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return 0;
        }

        var scaled = seconds * 1000.0;
        if (scaled >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)scaled;
    }
}
