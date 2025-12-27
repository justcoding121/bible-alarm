#nullable enable
using System.Diagnostics;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for tracking bootstrap timing from app launch to home page ready.
/// </summary>
public static class BootstrapTimingHelper
{
    private static readonly Stopwatch appLaunchStopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Gets the elapsed time in milliseconds since app launch.
    /// </summary>
    public static long GetElapsedMilliseconds() => appLaunchStopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Gets a high-precision timestamp that can be used to calculate elapsed time.
    /// </summary>
    public static long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Converts a timestamp difference to milliseconds.
    /// </summary>
    public static double TimestampToMilliseconds(long startTimestamp, long endTimestamp)
    {
        return (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}

