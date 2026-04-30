using Bible.Alarm.Shared.Models.Enums;
using Foundation;

namespace Bible.Alarm.Platforms.iOS.Extensions;

public static class DateExtensions
{
    public static NSDateComponents ToNsDateComponents(this DateTime date, nint dayOfWeek)
    {
        return new NSDateComponents
        {
            Hour = date.Hour,
            Minute = date.Minute,
            Second = date.Second,
            Weekday = dayOfWeek
        };
    }

    public static List<nint> ToWeekDays(this WeekDays daysOfWeek)
    {
        var result = new List<nint>();

        if ((daysOfWeek & WeekDays.Sunday) == WeekDays.Sunday)
        {
            result.Add(1);
        }

        if ((daysOfWeek & WeekDays.Monday) == WeekDays.Monday)
        {
            result.Add(2);
        }

        if ((daysOfWeek & WeekDays.Tuesday) == WeekDays.Tuesday)
        {
            result.Add(3);
        }

        if ((daysOfWeek & WeekDays.Wednesday) == WeekDays.Wednesday)
        {
            result.Add(4);
        }

        if ((daysOfWeek & WeekDays.Thursday) == WeekDays.Thursday)
        {
            result.Add(5);
        }

        if ((daysOfWeek & WeekDays.Friday) == WeekDays.Friday)
        {
            result.Add(6);
        }

        if ((daysOfWeek & WeekDays.Saturday) == WeekDays.Saturday)
        {
            result.Add(7);
        }

        return result;
    }
}
