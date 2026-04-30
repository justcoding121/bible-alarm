using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Models.Enums;

/// <summary>
/// Bitmask of selected weekdays for alarm recurrence (Sonar S2342: name must end with plural "s").
/// Schedule models expose this as <see cref="AlarmSchedule.DaysOfWeek"/> (property name unchanged).
/// </summary>
[Flags]
public enum WeekDays
{
    Sunday = 1,
    Monday = 2,
    Tuesday = 4,
    Wednesday = 8,
    Thursday = 16,
    Friday = 32,
    Saturday = 64,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
}

public static class WeekDaysExtensions
{
    public static List<int> ToList(this WeekDays daysOfWeek)
    {
        var result = new List<int>();

        var day = 1;
        foreach (var item in Enum.GetValues<WeekDays>())
        {
            if (item == WeekDays.All)
            {
                continue;
            }

            if ((daysOfWeek & item) == item)
            {
                result.Add(day);
            }

            day++;
        }

        return result;
    }
}
