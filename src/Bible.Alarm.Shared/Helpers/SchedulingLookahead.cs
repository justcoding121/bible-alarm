#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Builds lookahead spans for occurrence enumeration from non-negative day counts.
/// </summary>
public static class SchedulingLookahead
{
    public static TimeSpan FromDaysOrZero(int days) =>
        days <= 0 ? TimeSpan.Zero : TimeSpan.FromDays(days);
}
