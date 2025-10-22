using Foundation;

namespace Bible.Alarm.Platforms.iOS.Extensions
{
    public static class DateExtensions
    {
        public static NSDateComponents ToNsDateComponents(this DateTime date, nint dayOfWeek)
        {
            return new NSDateComponents()
            {
                Hour = date.Hour,
                Minute = date.Minute,
                Second = date.Second,
                Weekday = dayOfWeek
            };
        }

        public static List<nint> ToWeekDays(this DaysOfWeek daysOfWeek)
        {
            var result = new List<nint>();

            if ((daysOfWeek & DaysOfWeek.Sunday) == DaysOfWeek.Sunday)
            {
                result.Add(1);
            }

            if ((daysOfWeek & DaysOfWeek.Monday) == DaysOfWeek.Monday)
            {
                result.Add(2);
            }

            if ((daysOfWeek & DaysOfWeek.Tuesday) == DaysOfWeek.Tuesday)
            {
                result.Add(3);
            }

            if ((daysOfWeek & DaysOfWeek.Wednesday) == DaysOfWeek.Wednesday)
            {
                result.Add(4);
            }

            if ((daysOfWeek & DaysOfWeek.Thursday) == DaysOfWeek.Thursday)
            {
                result.Add(5);
            }

            if ((daysOfWeek & DaysOfWeek.Friday) == DaysOfWeek.Friday)
            {
                result.Add(6);
            }

            if ((daysOfWeek & DaysOfWeek.Saturday) == DaysOfWeek.Saturday)
            {
                result.Add(7);
            }

            return result;
        }
    }
}