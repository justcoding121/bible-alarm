#nullable enable
using Bible;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

public static class SchedulePropertyHelper
{
    public static string GetName(ScheduleStateItem? currentSchedule) => currentSchedule?.Name ?? string.Empty;

    public static bool GetIsEnabled(ScheduleStateItem? currentSchedule) => currentSchedule?.IsEnabled ?? false;

    public static DaysOfWeek GetDaysOfWeek(ScheduleStateItem? currentSchedule) => currentSchedule?.DaysOfWeek ?? 0;

    public static TimeSpan GetTime(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule != null)
        {
            return new TimeSpan(currentSchedule.Hour, currentSchedule.Minute, currentSchedule.Second);
        }
        return TimeSpan.Zero;
    }

    public static bool GetMusicEnabled(ScheduleStateItem? currentSchedule) => currentSchedule?.MusicEnabled ?? false;

    public static int GetScheduleId(ScheduleStateItem? currentSchedule) => currentSchedule?.Id ?? 0;
}

