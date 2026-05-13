#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Pure matching rules for Windows scheduled toast notification IDs (legacy id-only or "{scheduleId}_{suffix}" shape).
/// </summary>
public static class ScheduledToastNotificationIdMatcher
{
    public static bool MatchesSchedule(int scheduleId, string toastId)
    {
        ArgumentNullException.ThrowIfNull(toastId);

        var scheduleIdText = scheduleId.ToString();
        return toastId == scheduleIdText
               || toastId.StartsWith($"{scheduleIdText}_", StringComparison.Ordinal);
    }
}
